import test from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { logisticsFixture, economyFixture } from './logistics.fixtures.mjs';
import { assertLocalEnvironment, LOCAL_PROJECT } from '../functions/lib/localEnvironment.js';
assertLocalEnvironment(process.env);
const { Timestamp } = await import('firebase-admin/firestore');
const { createLocalFirestore, initializeIdentity } = await import('../functions/lib/identity.js');
const { createCompanyCommand, companyIdForUid, readFoundation } = await import('../functions/lib/company.js');
const { runEconomyCommand } = await import('../functions/lib/economy.js');
const { provisionStarterFleet, assignmentCommand, assignmentId, writeAssignment } = await import('../functions/lib/fleet.js');
const { createSyntheticWarehouse } = await import('../functions/lib/warehouses.js');
const { readMyFleetState, readMyWarehouseState } = await import('../functions/lib/logisticsReads.js');
const { digest, warehouseId } = await import('../functions/lib/logisticsModel.js');
const request = (key,payload={}) => ({protocolVersion:1,idempotencyKey:key,payload});
const readRequest={protocolVersion:1};
async function authPost(method,body) {
  const response=await fetch(`http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:${method}?key=synthetic-key`,{
    method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body),signal:AbortSignal.timeout(15000)});
  assert.equal(response.status,200);return response.json();
}
async function callable(name,data,token) {
  const response=await fetch(`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/${name}`,{
    method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:JSON.stringify({data}),signal:AbortSignal.timeout(15000)});
  return {status:response.status,body:await response.json()};
}
test('logistics: starter uniqueness, atomic reservations, warehouses and safe bounded APIs', {timeout:240000}, async t=>{
  const previous=process.env.FUNCTIONS_EMULATOR;process.env.FUNCTIONS_EMULATOR='true';
  const db=createLocalFirestore();const refs=new Map();
  const own=path=>{const ref=db.doc(path);refs.set(path,ref);return ref;};
  let token;
  try {
    const signup=await authPost('signUp',{returnSecureToken:true});token=signup.idToken;
    const uid=signup.localId, auth={uid}, companyId=companyIdForUid(uid), root=`v2Companies/${companyId}`;
    const player=own(`v2Players/${uid}`), company=own(root);
    const marker=own(`${root}/provisioning/starterFleet`), truck=own(`${root}/assets/starter_truck`), driver=own(`${root}/assets/starter_driver`);
    const version=`synthetic_${randomUUID()}`, catalog=logisticsFixture(version), catalogRef=own(`v2LogisticsCatalog/${version}`);
    const configRef=own(`v2WorldConfig/${version}`), finance=own(`${root}/state/finance`), progression=own(`${root}/state/progression`);
    const receipt=key=>own(`v2Players/${uid}/commands/${key}`);
    const provision=key=>{receipt(key);return provisionStarterFleet(db,auth,request(key),version);};
    const selected=(truckId='starter_truck',driverId='starter_driver',trailerId)=>[
      {assetId:truckId,kind:'truck'},{assetId:driverId,kind:'driver'},...(trailerId?[{assetId:trailerId,kind:'trailer'}]:[])];
    const assign=(key,action,source,assets=selected())=>{
      receipt(key);own(`${root}/assignments/${assignmentId(companyId,source)}`);
      return assignmentCommand(db,auth,request(key),action,source,assets);
    };
    const snapshot=async list=>(await db.getAll(...list)).map(d=>({data:d.data(),time:d.updateTime?.toMillis()}));
    const assetFixture=async(id,kind)=>{
      const ref=own(`${root}/assets/${id}`);
      await ref.set({schemaVersion:1,assetId:id,kind,companyId,catalogId:`test_${kind}`,catalogVersion:version,catalogHash:digest(catalog),
        status:'operational',assignmentId:null,acquiredAt:Timestamp.now()});return ref;
    };
    await initializeIdentity(db,auth,readRequest);receipt('create');await createCompanyCommand(db,auth,request('create',{name:'Synthetic Logistics'}));
    await catalogRef.set(catalog);
    await t.test('pending economy, missing catalog, invalid auth and payload cannot provision',async()=>{
      await assert.rejects(provision('pending'));
      assert.equal((await truck.get()).exists,false);assert.equal((await marker.get()).exists,false);
      assert.deepEqual((await readMyFleetState(db,auth,readRequest)).result,{hasCompany:true,economyReady:false,starterFleetReady:false,items:[],nextCursor:null});
      await assert.rejects(provisionStarterFleet(db,undefined,request('unauth'),version));
      receipt('spoof');await assert.rejects(provisionStarterFleet(db,auth,request('spoof',{companyId:'foreign'}),version));
      await configRef.set(economyFixture(version));receipt('init');
      const initialized=await runEconomyCommand(db,auth,request('init'),{kind:'initialize',id:version});
      own(`${root}/ledger/${initialized.result.operationId}`);own(`${root}/claims/initialization`);
      await catalogRef.delete();await assert.rejects(provision('missing_catalog'));
      await catalogRef.set({...catalog,approval:'unapproved'});await assert.rejects(provision('bad_catalog'));
      await catalogRef.set(catalog);
      const before=await snapshot([company,finance,progression]);
      assert.equal((await readMyFleetState(db,auth,readRequest)).result.starterFleetReady,false);
      assert.deepEqual(await snapshot([company,finance,progression]),before);
    });
    await t.test('partial preexisting starter, owner and migration blockers fail closed without legacy writes',async()=>{
      await assetFixture('starter_truck','truck');await assert.rejects(provision('partial'));await truck.delete();
      await marker.set({schemaVersion:1});await assert.rejects(provision('corrupt_marker'));await marker.delete();
      const original=(await company.get()).data();await company.update({ownerUid:'foreign'});await assert.rejects(provision('foreign'));await company.set(original);
      const originalPlayer=(await player.get()).data();await player.update({companyId:'foreign'});await assert.rejects(provision('mapping'));await player.set(originalPlayer);
      const legacy=own(`users/${uid}`);await legacy.set({synthetic:true,money:71});const before=await snapshot([legacy]);
      await assert.rejects(provision('legacy'));assert.deepEqual(await snapshot([legacy]),before);await legacy.delete();
      const migration=own(`v2MigrationRecords/${uid}`);await migration.set({synthetic:true,status:'pending'});
      await assert.rejects(provision('migration'));await migration.delete();
      assert.equal((await driver.get()).exists,false);
    });
    let grant;
    await t.test('eight concurrent different keys grant exactly one truck/driver with permanent marker and stable replay',async()=>{
      const before=await snapshot([company,finance,progression]);
      const outcomes=await Promise.allSettled(Array.from({length:8},(_,i)=>provision(`starter_${i}`)));
      assert.equal(outcomes.filter(o=>o.status==='fulfilled').length,1);
      const winner=outcomes.findIndex(o=>o.status==='fulfilled');grant=outcomes[winner].value;
      assert.deepEqual(await provision(`starter_${winner}`),grant);
      await assert.rejects(provision('different_key'));
      assert.equal((await db.collection(`${root}/assets`).limit(4).get()).size,2);
      assert.equal((await db.collection(`${root}/warehouses`).limit(1).get()).empty,true);
      assert.equal((await truck.get()).data().companyId,companyId);assert.equal((await driver.get()).data().kind,'driver');
      assert.deepEqual(await snapshot([company,finance,progression]),before);
      for(let i=0;i<8;i++)assert.equal((await receipt(`starter_${i}`).get()).exists,i===winner);
      assert.equal((await readMyFleetState(db,auth,readRequest)).result.starterFleetReady,true);
    });
    await t.test('missing/corrupt post-grant state fails closed even on receipt replay',async()=>{
      const originalTruck=(await truck.get()).data(),originalMarker=(await marker.get()).data();
      for(const patch of [{kind:'trailer'},{companyId:'foreign'},{catalogId:'unknown'},{status:'invalid'},{assignmentId:'bad'},{schemaVersion:2}]) {
        await truck.set({...originalTruck,...patch});const before=await snapshot([truck,driver,marker]);
        await assert.rejects(readMyFleetState(db,auth,readRequest));await assert.rejects(provision('corrupt_regrant'));assert.deepEqual(await snapshot([truck,driver,marker]),before);
      }
      await truck.delete();await assert.rejects(readMyFleetState(db,auth,readRequest));
      const winnerKey=(await db.collection(`v2Players/${uid}/commands`).where('command','==','provisionStarterFleet').limit(1).get()).docs[0].id;
      await assert.rejects(provision(winnerKey));await truck.set(originalTruck);
      await marker.delete();await assert.rejects(provision('missing_marker'));await marker.set(originalMarker);
      await catalogRef.set({...catalog,starter:{...catalog.starter,packageVersion:'changed'}});await assert.rejects(readMyFleetState(db,auth,readRequest));await catalogRef.set(catalog);
    });
    await t.test('reservation/release pair is atomic, stale ownership cannot clear another assignment',async()=>{
      const trailer=await assetFixture('test_optional_trailer','trailer');
      const assets=selected('starter_truck','starter_driver',trailer.id);
      const beforeEconomy=await snapshot([finance,progression]);
      const reserved=await assign('reserve','reserve','synthetic_delivery',assets);
      assert.deepEqual(await assign('reserve','reserve','synthetic_delivery',assets),reserved);
      for(const ref of [truck,driver,trailer])assert.equal((await ref.get()).data().assignmentId,reserved.result.assignmentId);
      const before=await snapshot([truck,driver,trailer]);
      await assert.rejects(assign('wrong_release','release','other_delivery',assets));assert.deepEqual(await snapshot([truck,driver,trailer]),before);
      const released=await assign('release','release','synthetic_delivery',assets);
      assert.deepEqual(await assign('release','release','synthetic_delivery',assets),released);
      await assert.rejects(assign('reuse_delivery','reserve','synthetic_delivery',assets));
      await assign('reserve_new','reserve','next_delivery',assets);
      await assert.rejects(assign('stale_release','release','synthetic_delivery',assets));
      assert.equal((await truck.get()).data().assignmentId,assignmentId(companyId,'next_delivery'));
      await assign('release_new','release','next_delivery',assets);
      assert.deepEqual(await snapshot([finance,progression]),beforeEconomy);
    });
    await t.test('real concurrent assignments sharing truck or driver have one winner and no partial reservation',async()=>{
      const otherTruck=await assetFixture('other_truck','truck'),otherDriver=await assetFixture('other_driver','driver');
      for(const mode of ['truck','driver']) {
        const pairs=mode==='truck'?[selected(),selected('starter_truck','other_driver')]:[selected(),selected('other_truck','starter_driver')];
        const outcomes=await Promise.allSettled(pairs.map((pair,i)=>assign(`${mode}_${i}`,'reserve',`${mode}_operation_${i}`,pair)));
        assert.equal(outcomes.filter(x=>x.status==='fulfilled').length,1);
        const winner=outcomes.findIndex(x=>x.status==='fulfilled');
        const loser=1-winner;assert.equal((await receipt(`${mode}_${loser}`).get()).exists,false);
        const loserOnly=pairs[loser].find(e=>!pairs[winner].some(w=>w.assetId===e.assetId));
        assert.equal((await own(`${root}/assets/${loserOnly.assetId}`).get()).data().assignmentId,null);
        await assign(`${mode}_release`,'release',`${mode}_operation_${winner}`,pairs[winner]);
        assert.equal((await otherTruck.get()).data().assignmentId,null);assert.equal((await otherDriver.get()).data().assignmentId,null);
      }
    });
    await t.test('transaction abort, unavailable assets and wrong expected kinds leave no reservation or receipt',async()=>{
      const before=await snapshot([truck,driver]);const rollbackRef=own(`${root}/assignments/${assignmentId(companyId,'rollback')}`);
      await assert.rejects(db.runTransaction(async tx=>{
        const state=await readFoundation(tx,db,uid);await writeAssignment(tx,db,state,selected(),'rollback','reserve');throw Error('synthetic rollback');
      }));
      assert.deepEqual(await snapshot([truck,driver]),before);assert.equal((await rollbackRef.get()).exists,false);
      await driver.update({status:'out_of_service'});await assert.rejects(assign('unavailable','reserve','unavailable'));
      assert.equal((await truck.get()).data().assignmentId,null);await driver.update({status:'operational'});
      await assert.rejects(assign('wrong_kind','reserve','wrong_kind',selected('starter_driver','starter_truck')));
      await assert.rejects(assign('missing_asset','reserve','missing_asset',selected('missing','starter_driver')));
      assert.equal((await receipt('wrong_kind').get()).exists,false);
    });
    await t.test('warehouse synthetic creation, uniqueness and ownership validate without granting starter warehouse',async()=>{
      assert.deepEqual((await readMyWarehouseState(db,auth,readRequest)).result.items,[]);
      const def=catalog.warehouses[0],ref=own(`${root}/warehouses/${warehouseId(def.cityId,def.type)}`);
      const create=key=>{receipt(key);return createSyntheticWarehouse(db,auth,request(key),version,def.id);};
      const before=await snapshot([finance,progression]);const created=await create('warehouse');assert.deepEqual(await create('warehouse'),created);
      await assert.rejects(create('duplicate_warehouse'));assert.deepEqual(await snapshot([finance,progression]),before);
      const original=(await ref.get()).data();await ref.update({companyId:'foreign'});
      await assert.rejects(readMyWarehouseState(db,auth,readRequest));await assert.rejects(create('foreign_warehouse'));await ref.set(original);
      await ref.update({usedCapacity:11});await assert.rejects(readMyWarehouseState(db,auth,readRequest));await ref.set(original);
      const warehouse=(await readMyWarehouseState(db,auth,readRequest)).result.items[0];assert.equal(warehouse.capacityUnits,'10');
      receipt('unknown_warehouse');await assert.rejects(createSyntheticWarehouse(db,auth,request('unknown_warehouse'),version,'unknown'));
    });
    await t.test('bounded pagination, authorization-safe callables and read-only snapshots',async()=>{
      for(let i=0;i<23;i++)await assetFixture(`zz_fixture_${String(i).padStart(2,'0')}`,'truck');
      const before=await snapshot([...refs.values()]);
      let after;const ids=[];let pages=0;
      do {
        const response=await readMyFleetState(db,auth,{protocolVersion:1,limit:7,...(after?{after}:{})});
        assert.ok(response.result.items.length<=7);ids.push(...response.result.items.map(i=>i.assetId));after=response.result.nextCursor;pages++;
      }while(after && pages<10);
      assert.equal(ids.length,28);assert.equal(new Set(ids).size,ids.length);assert.equal(after,null);
      for(const name of ['getMyFleetState','getMyWarehouseState']) {
        assert.equal((await callable(name,readRequest)).status,401);
        assert.equal((await callable(name,readRequest,'invalid')).status,401);
        for(const extra of [{uid:'foreign'},{companyId:'foreign'},{limit:21},{after:'../foreign'}]) assert.equal((await callable(name,{...readRequest,...extra},token)).status,400);
        const response=await callable(name,readRequest,token);assert.equal(response.status,200);assert.ok(response.body.result.result.items.length<=20);
      }
      const response=await callable('getMyFleetState',readRequest,token);assert.equal(response.body.result.result.starterFleetReady,true);
      assert.deepEqual(await snapshot([...refs.values()]),before);
    });
  } finally {
    if(token)await authPost('delete',{idToken:token});
    for(const ref of refs.values())await ref.delete();
    await db.terminate();if(previous===undefined)delete process.env.FUNCTIONS_EMULATOR;else process.env.FUNCTIONS_EMULATOR=previous;
  }
});

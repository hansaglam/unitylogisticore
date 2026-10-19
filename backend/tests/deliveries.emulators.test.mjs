import test from 'node:test';
import assert from 'node:assert/strict';
import {randomUUID,createHash} from 'node:crypto';
import {assertLocalEnvironment,LOCAL_PROJECT} from '../functions/lib/localEnvironment.js';
assertLocalEnvironment(process.env);
const {Timestamp}=await import('firebase-admin/firestore');
const {createLocalFirestore,initializeIdentity}=await import('../functions/lib/identity.js');
const {companyIdForUid,createCompanyCommand}=await import('../functions/lib/company.js');
const {runEconomyCommand}=await import('../functions/lib/economy.js');
const {provisionStarterFleet,assignmentId}=await import('../functions/lib/fleet.js');
const {generateContractOffers,acceptContractCommand,readMyContracts}=await import('../functions/lib/contracts.js');
const {startDeliveryCommand,claimDeliveryRewardCommand,readMyDeliveries}=await import('../functions/lib/deliveries.js');
const {deliveryIdFor}=await import('../functions/lib/deliveryModel.js');
const {digest}=await import('../functions/lib/logisticsModel.js');
const {contractFixtures,candidates}=await import('./contracts.fixtures.mjs');
const req=(key,payload={})=>({protocolVersion:1,idempotencyKey:key,payload});
async function post(url,body,token){const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:JSON.stringify(body),signal:AbortSignal.timeout(20000)});return {status:r.status,body:await r.json()};}
test('deliveries: atomic start, trusted arrival, permanent reward and reservation settlement',{timeout:240000},async t=>{
 const previous=process.env.FUNCTIONS_EMULATOR;process.env.FUNCTIONS_EMULATOR='true';const db=createLocalFirestore();const refs=new Map();let token;
 const own=path=>{const ref=db.doc(path);refs.set(path,ref);return ref;};
 try{
  const signup=await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=synthetic-key',{returnSecureToken:true});token=signup.body.idToken;
  const uid=signup.body.localId,auth={uid},cid=companyIdForUid(uid),root=`v2Companies/${cid}`,version=`synthetic_${randomUUID()}`,f=contractFixtures(version);
  const player=own(`v2Players/${uid}`),company=own(root),finance=own(`${root}/state/finance`),progression=own(`${root}/state/progression`);
  const truck=own(`${root}/assets/starter_truck`),driver=own(`${root}/assets/starter_driver`),marker=own(`${root}/provisioning/starterFleet`);
  const rulesRef=own(`v2DeliveryRules/${version}`);const receipt=key=>own(`v2Players/${uid}/commands/${key}`);
  const rules={schemaVersion:1,version,approval:'synthetic_emulator_only',contractConfigHash:digest(f.config),logisticsHash:digest(f.logistics),economyHash:f.config.economyHash,
   routes:[{routeId:'synthetic_route',cargoId:'synthetic_cargo',durationMs:1000,xp:10}]};
  await own(`v2WorldConfig/${version}`).set(f.economy);await own(`v2LogisticsCatalog/${version}`).set(f.logistics);await own(`v2ContractCatalog/${version}`).set(f.config);await rulesRef.set(rules);
  await initializeIdentity(db,auth,{protocolVersion:1});receipt('create');await createCompanyCommand(db,auth,req('create',{name:'Synthetic Deliveries'}));receipt('init');
  const init=await runEconomyCommand(db,auth,req('init'),{kind:'initialize',id:version});own(`${root}/ledger/${init.result.operationId}`);own(`${root}/claims/initialization`);
  receipt('starter');await provisionStarterFleet(db,auth,req('starter'),version);
  let now=Math.floor(Date.now()/600000)*600000+1000;const clock=()=>now;
  const snapshot=async list=>(await db.getAll(...list)).map(d=>({data:d.data(),time:d.updateTime?.toMillis()}));
  async function pool(label,assets){const epoch=Math.floor(now/600000);receipt(`gen_${label}`);own(`${root}/contractPools/epoch_${epoch}`);
   const generated=await generateContractOffers(db,auth,req(`gen_${label}`),{configVersion:version,epoch,assets},clock);
   const contracts=generated.result.contractIds.map(id=>own(`${root}/contracts/${id}`));
   const accepts=[];for(let i=0;i<2;i++){receipt(`accept_${label}_${i}`);accepts.push(await acceptContractCommand(db,auth,req(`accept_${label}_${i}`,{contractId:contracts[i].id}),clock));}
   return {contracts,accepts};
  }
  const first=await pool('first',candidates);
  const payload=(contractId,selection=candidates)=>({contractId,truckId:selection.find(a=>a.kind==='truck').assetId,driverId:selection.find(a=>a.kind==='driver').assetId});
  const track=id=>{own(`${root}/deliveries/${id}`);own(`${root}/assignments/${assignmentId(cid,id)}`);own(`${root}/claims/delivery_${id}`);own(`${root}/ledger/${createHash('sha256').update(`${cid}:delivery_${id}`).digest('hex')}`);};
  const start=(key,contract=first.contracts[0],selection=candidates,database=db)=>{receipt(key);track(deliveryIdFor(cid,contract.id));return startDeliveryCommand(database,auth,req(key,payload(contract.id,selection)),clock);};
  const claim=(key,id,database=db)=>{receipt(key);track(id);return claimDeliveryRewardCommand(database,auth,req(key,{deliveryId:id}),clock);};
  const fault=(kind)=>{let attempts=0;return {db:new Proxy(db,{get(target,key){if(key==='runTransaction')return callback=>target.runTransaction(async tx=>{const result=await callback(tx);attempts++;if(kind==='rollback')throw Error('synthetic rollback');if(attempts===1)throw Object.assign(Error('synthetic retry'),{code:10});return result;});const v=target[key];return typeof v==='function'?v.bind(target):v;}}),attempts:()=>attempts};};
  await t.test('auth, owner, migration, readiness, equipment, rules and client-clock spoofing fail closed',async()=>{
   await assert.rejects(startDeliveryCommand(db,undefined,req('unauth',payload(first.contracts[0].id)),clock));
   for(const key of ['uid','companyId','reward','expectedArrivalAt','startedAt','now'])await assert.rejects(startDeliveryCommand(db,auth,req('spoof',{...payload(first.contracts[0].id),[key]:1}),clock));
   const original=(await company.get()).data();await company.update({ownerUid:'foreign'});await assert.rejects(start('foreign'));await company.set(original);
   const legacy=own(`users/${uid}`);await legacy.set({money:17,synthetic:true});const before=await snapshot([legacy]);await assert.rejects(start('legacy'));assert.deepEqual(await snapshot([legacy]),before);await legacy.delete();
   const migration=own(`v2MigrationRecords/${uid}`);await migration.set({status:'pending'});await assert.rejects(start('migration'));await migration.delete();
   const identity=(await player.get()).data();await player.update({identityStatus:'suspended'});await assert.rejects(start('identity'));await player.set(identity);
   const m=(await marker.get()).data();await marker.delete();await assert.rejects(start('starter_missing'));await marker.set(m);
   const saved=(await finance.get()).data();await finance.delete();await assert.rejects(start('economy_missing'));await finance.set(saved);
   await assert.rejects(start('wrong_assets',first.contracts[0],[{assetId:'foreign',kind:'truck'},candidates[1]]));
   await rulesRef.delete();await assert.rejects(start('rules_missing'));await rulesRef.set({...rules,approval:'unapproved'});await assert.rejects(start('rules_bad'));await rulesRef.set(rules);
   assert.equal((await receipt('rules_bad').get()).exists,false);
  });
  let started,id;
  await t.test('start rollback, eight concurrent starts, no payout and stable receipt',async()=>{
   const before=await snapshot([finance,progression,truck,driver,first.contracts[0]]);
   await assert.rejects(start('rollback',first.contracts[0],candidates,fault('rollback').db));assert.deepEqual(await snapshot([finance,progression,truck,driver,first.contracts[0]]),before);
   const economyBefore=await snapshot([finance,progression]);const outcomes=await Promise.allSettled(Array.from({length:8},(_,i)=>start(`start_${i}`)));
   assert.equal(outcomes.filter(r=>r.status==='fulfilled').length,1);const winner=outcomes.findIndex(r=>r.status==='fulfilled');started=outcomes[winner].value;id=started.result.deliveryId;
   assert.deepEqual(await start(`start_${winner}`),started);await assert.rejects(start('duplicate'));await assert.rejects(start('same_equipment',first.contracts[1]));
   assert.deepEqual(await snapshot([finance,progression]),economyBefore);assert.equal((await truck.get()).data().assignmentId,assignmentId(cid,id));
  });
  await t.test('arrival boundary, immutable rules, overflow and corrupt assignment prevent all partial settlement',async()=>{
   const arrival=Date.parse(started.result.expectedArrivalAt);now=arrival-1;assert.equal((await readMyDeliveries(db,auth,{protocolVersion:1},clock)).result.items[0].arrivalReady,false);await assert.rejects(claim('early',id),e=>e.details.code==='DELIVERY_NOT_ARRIVED');
   await assert.rejects(claimDeliveryRewardCommand(db,auth,req('clock',{deliveryId:id,now:arrival+1}),clock));now=arrival;const activeBefore=await snapshot([...refs.values()]);assert.equal((await readMyDeliveries(db,auth,{protocolVersion:1},clock)).result.items[0].arrivalReady,true);assert.deepEqual(await snapshot([...refs.values()]),activeBefore);
   await rulesRef.set({...rules,routes:[{...rules.routes[0],durationMs:1}]});await assert.rejects(claim('rules_drift',id));await rulesRef.set(rules);
   const wallet=(await finance.get()).data();await finance.update({balance:f.economy.maxBalance});let before=await snapshot([finance,progression,truck,driver,first.contracts[0],own(`${root}/deliveries/${id}`)]);
   await assert.rejects(claim('overflow',id));assert.deepEqual(await snapshot([finance,progression,truck,driver,first.contracts[0],own(`${root}/deliveries/${id}`)]),before);await finance.set(wallet);
   await truck.update({assignmentId:'a'.repeat(64)});before=await snapshot([finance,progression,truck,driver]);await assert.rejects(claim('corrupt',id));assert.deepEqual(await snapshot([finance,progression,truck,driver]),before);await truck.update({assignmentId:assignmentId(cid,id)});
   const beforeRollback=await snapshot([...refs.values()]);await assert.rejects(claim('claim_rollback',id,fault('rollback').db));assert.deepEqual(await snapshot([...refs.values()].filter(r=>!r.path.endsWith('/claim_rollback'))),beforeRollback);
  });
  let paid;
  await t.test('eight actual concurrent claims atomically pay once, release exact assets and preserve history',async()=>{
   const outcomes=await Promise.allSettled(Array.from({length:8},(_,i)=>claim(`claim_${i}`,id)));assert.equal(outcomes.filter(r=>r.status==='fulfilled').length,1);
   const winner=outcomes.findIndex(r=>r.status==='fulfilled');paid=outcomes[winner].value;assert.deepEqual(await claim(`claim_${winner}`,id),paid);await assert.rejects(claim('duplicate_reward',id));
   assert.equal((await finance.get()).data().balance,106);assert.equal((await progression.get()).data().xp,10);assert.equal((await progression.get()).data().level,2);
   const ledger=(await own(`${root}/ledger/${paid.result.operationId}`).get()).data();assert.equal(ledger.delta,6);assert.equal(ledger.balanceAfter,106);
   assert.equal((await truck.get()).data().assignmentId,null);assert.equal((await driver.get()).data().assignmentId,null);assert.equal((await first.contracts[0].get()).data().status,'completed');
   const startReceipt=(await db.collection(`v2Players/${uid}/commands`).where('command','==','startDelivery').limit(1).get()).docs[0];assert.deepEqual((await start(startReceipt.id)).result,started.result);
   assert.deepEqual(await acceptContractCommand(db,auth,req('accept_first_0',{contractId:first.contracts[0].id}),clock),first.accepts[0]);
   assert.equal((await readMyContracts(db,auth,{protocolVersion:1},clock)).result.items.find(c=>c.contractId===first.contracts[0].id).status,'completed');
  });
  await t.test('SDK retry, lost response recovery and stale completed claim cannot release a newer delivery',async()=>{
   const retry=fault('retry');const second=await start('retry_start',first.contracts[1],candidates,retry.db);assert.ok(retry.attempts()>=2);const secondId=second.result.deliveryId;
   const before=await snapshot([truck,driver,finance,progression]);await assert.rejects(claim('stale',id));assert.deepEqual(await snapshot([truck,driver,finance,progression]),before);
   now=Date.parse(second.result.expectedArrivalAt);
   const realCollection=db.collection.bind(db);let lose=true;
   const lossy=new Proxy(db,{get(target,key){if(key==='collection')return path=>{const c=realCollection(path);if(path!=='v2Players')return c;const doc=c.doc.bind(c);c.doc=uid=>{const p=doc(uid),sub=p.collection.bind(p);p.collection=name=>{const s=sub(name),d=s.doc.bind(s);s.doc=k=>{const ref=d(k),get=ref.get.bind(ref);ref.get=async(...args)=>{if(k==='lost'&&lose){lose=false;throw Object.assign(Error('synthetic lost response'),{code:14});}return get(...args);};return ref;};return s;};return p;};return c;};const v=target[key];return typeof v==='function'?v.bind(target):v;}});
   await assert.rejects(claim('lost',secondId,lossy),e=>e.details.code==='RETRY_LATER');const recovery=await claim('lost',secondId);assert.equal(recovery.result.balance,'112');assert.equal((await progression.get()).data().xp,20);
  });
  await t.test('separate real races sharing only truck or only driver produce one active delivery',async()=>{
   for(const kind of ['truck','driver'])await own(`${root}/assets/other_${kind}`).set({schemaVersion:1,assetId:`other_${kind}`,kind,companyId:cid,catalogId:`test_${kind}`,catalogVersion:version,catalogHash:digest(f.logistics),status:'operational',assignmentId:null,acquiredAt:Timestamp.now()});
   for(const shared of ['truck','driver']){
    now=(Math.floor(now/600000)+1)*600000+1000;const a=await pool(`${shared}_a`,candidates);
    const alternative=shared==='truck'?[candidates[0],{assetId:'other_driver',kind:'driver'}]:[{assetId:'other_truck',kind:'truck'},candidates[1]];
    now+=600000;const b=await pool(`${shared}_b`,alternative);
    const results=await Promise.allSettled([start(`${shared}_start_a`,a.contracts[0]),start(`${shared}_start_b`,b.contracts[0],alternative)]);
    assert.equal(results.filter(r=>r.status==='fulfilled').length,1);const successful=results.find(r=>r.status==='fulfilled').value;
    now=Date.parse(successful.result.expectedArrivalAt);await claim(`${shared}_claim`,successful.result.deliveryId);
   }
  });
  await t.test('read-only bounded delivery DTOs, actual callable auth/replay and foreign access',async()=>{
   const endpoint=name=>`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/${name}`;
   for(const name of ['startDelivery','claimDeliveryReward','getMyDeliveries'])assert.equal((await post(endpoint(name),{data:{protocolVersion:1}})).status,401);
   for(const extra of [{uid:'other'},{companyId:'other'},{limit:21},{after:'../foreign'},{status:'active'}])assert.equal((await post(endpoint('getMyDeliveries'),{data:{protocolVersion:1,...extra}},token)).status,400);
   const before=await snapshot([...refs.values()]);let after,ids=[];do{const result=await readMyDeliveries(db,auth,{protocolVersion:1,limit:1,...(after?{after}:{})},clock);assert.ok(result.result.items.length<=1);ids.push(...result.result.items.map(d=>d.deliveryId));after=result.result.nextCursor;}while(after);
   assert.equal(ids.length,4);assert.equal(new Set(ids).size,4);assert.deepEqual(await snapshot([...refs.values()]),before);
   const read=await post(endpoint('getMyDeliveries'),{data:{protocolVersion:1}},token);assert.equal(read.status,200);assert.equal(read.body.result.result.items[0].reward,'6');
   const replay=await post(endpoint('claimDeliveryReward'),{data:req('lost',{deliveryId:deliveryIdFor(cid,first.contracts[1].id)})},token);assert.equal(replay.status,200);assert.equal(replay.body.result.result.balance,'112');
   const foreignUid=`synthetic_${randomUUID()}`,foreign={uid:foreignUid};own(`v2Players/${foreignUid}`);own(`v2Companies/${companyIdForUid(foreignUid)}`);own(`v2Players/${foreignUid}/commands/create`);
   await initializeIdentity(db,foreign,{protocolVersion:1});await createCompanyCommand(db,foreign,req('create',{name:'Foreign Synthetic'}));assert.deepEqual((await readMyDeliveries(db,foreign,{protocolVersion:1},clock)).result.items,[]);
   await assert.rejects(claimDeliveryRewardCommand(db,foreign,req('cross',{deliveryId:id}),clock));
  });
 }finally{if(token)await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:delete?key=synthetic-key',{idToken:token});for(const ref of refs.values())await ref.delete();await db.terminate();if(previous===undefined)delete process.env.FUNCTIONS_EMULATOR;else process.env.FUNCTIONS_EMULATOR=previous;}
});

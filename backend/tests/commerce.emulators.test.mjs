import test from 'node:test';
import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {assertLocalEnvironment,LOCAL_PROJECT} from '../functions/lib/localEnvironment.js';
assertLocalEnvironment(process.env);
const {Timestamp}=await import('firebase-admin/firestore');
const {createLocalFirestore,initializeIdentity}=await import('../functions/lib/identity.js');
const {companyIdForUid,createCompanyCommand}=await import('../functions/lib/company.js');
const {runEconomyCommand,operationId}=await import('../functions/lib/economy.js');
const {provisionStarterFleet,assignmentId}=await import('../functions/lib/fleet.js');
const {acquisitionCommand,readMyAcquisitionOffers}=await import('../functions/lib/commerce.js');
const {acquisitionId,acquisitionSource}=await import('../functions/lib/commerceModel.js');
const {readMyFleetState,readMyWarehouseState}=await import('../functions/lib/logisticsReads.js');
const {generateContractOffers,acceptContractCommand}=await import('../functions/lib/contracts.js');
const {startDeliveryCommand,claimDeliveryRewardCommand}=await import('../functions/lib/deliveries.js');
const {deliveryIdFor}=await import('../functions/lib/deliveryModel.js');
const {digest,warehouseId}=await import('../functions/lib/logisticsModel.js');
const {commerceFixtures}=await import('./commerce.fixtures.mjs');
const req=(key,offerId)=>({protocolVersion:1,idempotencyKey:key,payload:{offerId}});
const base=(key,payload={})=>({protocolVersion:1,idempotencyKey:key,payload});
async function post(url,body,token){const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:JSON.stringify(body),signal:AbortSignal.timeout(20000)});return {status:r.status,body:await r.json()};}
test('commerce: authoritative acquisition, atomic spending and compatible warehouse upgrades',{timeout:300000},async t=>{
 const previous=process.env.FUNCTIONS_EMULATOR;process.env.FUNCTIONS_EMULATOR='true';const db=createLocalFirestore(),refs=new Map();let token;
 const own=path=>{const ref=db.doc(path);refs.set(path,ref);return ref;};
 try{
  const signup=await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=synthetic-key',{returnSecureToken:true});token=signup.body.idToken;
  const uid=signup.body.localId,auth={uid},cid=companyIdForUid(uid),root=`v2Companies/${cid}`,version=`synthetic_${randomUUID()}`,f=commerceFixtures(version);
  const player=own(`v2Players/${uid}`),company=own(root),finance=own(`${root}/state/finance`),progression=own(`${root}/state/progression`);
  const truck=own(`${root}/assets/starter_truck`),driver=own(`${root}/assets/starter_driver`),marker=own(`${root}/provisioning/starterFleet`);
  const commerce=own(`v2CommerceCatalog/${version}`),logistics=own(`v2LogisticsCatalog/${version}`);
  const receipt=key=>own(`v2Players/${uid}/commands/${key}`),snapshot=async list=>(await db.getAll(...list)).map(d=>({data:d.data(),time:d.updateTime?.toMillis()}));
  await own(`v2WorldConfig/${version}`).set(f.economy);await logistics.set(f.logistics);await commerce.set(f.commerce);
  await initializeIdentity(db,auth,{protocolVersion:1});receipt('create');await createCompanyCommand(db,auth,base('create',{name:'Synthetic Commerce'}));receipt('init');
  const init=await runEconomyCommand(db,auth,base('init'),{kind:'initialize',id:version});own(`${root}/ledger/${init.result.operationId}`);own(`${root}/claims/initialization`);
  receipt('starter');await provisionStarterFleet(db,auth,base('starter'),version);
  let now=Date.now();const clock=()=>now,issued=new Map();
  async function offer(id,itemId='truck',changes={}){
   const item=f.commerce.items.find(i=>i.id===itemId),w=f.logistics.warehouses.find(w=>w.id===item.catalogId),isWarehouse=['openWarehouse','upgradeWarehouse'].includes(item.kind);
   const targetId=isWarehouse?warehouseId(w.cityId,w.type):acquisitionId(cid,id),sourceId=acquisitionSource(cid,id);
   const raw={schemaVersion:1,offerId:id,companyId:cid,catalogVersion:version,catalogHash:digest(f.commerce),itemId,kind:item.kind,price:item.price,currency:f.economy.currency,scale:f.economy.scale,
    requiredLevel:item.requiredLevel,expectedLevel:item.expectedLevel,targetId,sourceId,createdAt:Timestamp.fromMillis(now-1000),expiresAt:Timestamp.fromMillis(now+600000),status:'available',...changes};
   const ref=own(`${root}/acquisitionOffers/${id}`),target=own(`${root}/${isWarehouse?'warehouses':'assets'}/${targetId}`);
   own(`${root}/claims/${sourceId}`);own(`${root}/ledger/${operationId(cid,sourceId)}`);await ref.set(raw);issued.set(id,{ref,target,raw,kind:item.kind});return issued.get(id);
  }
  const buy=(key,id,database=db,kind=issued.get(id).kind)=>{receipt(key);return acquisitionCommand(database,auth,req(key,id),kind,clock);};
  const unchangedReject=async(key,id)=>{receipt(key);const before=await snapshot([...refs.values()]);await assert.rejects(buy(key,id));assert.deepEqual(await snapshot([...refs.values()]),before);};
  const fault=mode=>{let attempts=0;return {db:new Proxy(db,{get(target,key){if(key==='runTransaction')return callback=>target.runTransaction(async tx=>{const result=await callback(tx);attempts++;if(mode==='rollback')throw Error('synthetic rollback');if(attempts===1)throw Object.assign(Error('synthetic retry'),{code:10});return result;});const v=target[key];return typeof v==='function'?v.bind(target):v;}}),attempts:()=>attempts};};
  const starterBefore=await snapshot([truck,driver,marker]);
  await t.test('five valid acquisitions, exact debit and canonical reads with stable warehouse ID',async()=>{
   assert.deepEqual((await readMyWarehouseState(db,auth,{protocolVersion:1})).result.items,[]);
   const xpBefore=(await progression.get()).data().xp;
   for(const [id,item] of [['vehicle','truck'],['recruit','driver'],['equipment','trailer'],['depot','open']]){await offer(id,item);await buy(`buy_${id}`,id);}
   const warehouse=issued.get('depot').target;await warehouse.update({usedCapacity:7});const old=(await warehouse.get()).data();
   await offer('expand','upgrade');await buy('buy_expand','expand');const upgraded=(await warehouse.get()).data();
   assert.equal(upgraded.level,2);assert.equal(upgraded.usedCapacity,7);assert.deepEqual(upgraded.acquiredAt,old.acquiredAt);
   assert.equal((await readMyWarehouseState(db,auth,{protocolVersion:1})).result.items[0].capacityUnits,'20');
   const assets=(await readMyFleetState(db,auth,{protocolVersion:1})).result.items;assert.equal(assets.length,5);
   for(const id of ['vehicle','recruit','equipment'])assert.ok(assets.some(a=>a.assetId===issued.get(id).target.id&&a.available));
   assert.equal((await finance.get()).data().balance,940);assert.equal((await progression.get()).data().xp,xpBefore);
   for(const id of ['vehicle','recruit','equipment','depot','expand']){const o=(await issued.get(id).ref.get()).data(),l=(await own(`${root}/ledger/${o.operationId}`).get()).data();assert.equal(l.delta,-o.price);assert.equal(l.source,o.sourceId);assert.equal(o.status,'consumed');}
   assert.deepEqual(await snapshot([truck,driver,marker]),starterBefore);
  });
  await t.test('same-key historical replay/conflict, single-use offer and distinct same-model assets',async()=>{
   const original=await buy('buy_vehicle','vehicle');const before=await snapshot([...refs.values()]);assert.deepEqual(await buy('buy_vehicle','vehicle'),original);assert.deepEqual(await snapshot([...refs.values()]),before);
   await unchangedReject('again','vehicle');await offer('vehicle2');await assert.rejects(buy('buy_vehicle','vehicle2'),e=>e.details.code==='IDEMPOTENCY_CONFLICT');await buy('second','vehicle2');
   assert.notEqual(issued.get('vehicle').target.id,issued.get('vehicle2').target.id);
   await buy('buy_depot','depot'); // Historical opening replay after upgrade.
   const saved=(await issued.get('vehicle').target.get()).data();await issued.get('vehicle').target.delete();await unchangedReject('missing_asset','vehicle');await assert.rejects(buy('buy_vehicle','vehicle'));await issued.get('vehicle').target.set({...saved,companyId:'foreign'});await unchangedReject('corrupt_asset','vehicle');await issued.get('vehicle').target.set(saved);
  });
  await t.test('auth, spoofing, owner, readiness, migration, expired and invalid catalog/price rejection is atomic',async()=>{
   await offer('reject');const r=issued.get('reject');
   await assert.rejects(acquisitionCommand(db,undefined,req('unauth','reject'),'buyVehicle',clock));
   for(const field of ['price','ownerUid','companyId','level','now'])await assert.rejects(acquisitionCommand(db,auth,base('spoof',{offerId:'reject',[field]:1}),'buyVehicle',clock));
   for(const change of [{companyId:'foreign'},{price:-1},{price:Number.MAX_SAFE_INTEGER+1},{currency:'USD'},{scale:3},{targetId:'starter_truck'},
    {expiresAt:Timestamp.fromMillis(now)},{createdAt:Timestamp.fromMillis(now+1)}]){await r.ref.set({...r.raw,...change});await unchangedReject('invalid_offer','reject');}await r.ref.set(r.raw);
   const c=(await company.get()).data();await company.update({ownerUid:'foreign'});await unchangedReject('owner','reject');await company.set(c);
   for(const path of [`users/${uid}`,`v2MigrationRecords/${uid}`]){const ref=own(path);await ref.set({synthetic:true});await unchangedReject('migration','reject');await ref.delete();}
   const identity=(await player.get()).data();await player.update({identityStatus:'suspended'});await unchangedReject('identity','reject');await player.set(identity);
   const m=(await marker.get()).data();await marker.delete();await truck.delete();await driver.delete();await unchangedReject('not_ready','reject');await marker.set(m);await truck.set(starterBefore[0].data);await driver.set(starterBefore[1].data);
   const wallet=(await finance.get()).data();await finance.delete();await unchangedReject('no_economy','reject');await finance.set(wallet);
   await commerce.delete();await unchangedReject('missing_config','reject');
   for(const change of [{approval:'production'},{economyHash:'bad'},{rulesVersion:'bad'},{items:[{...f.commerce.items[0],price:-1}]}]){await commerce.set({...f.commerce,...change});await unchangedReject('invalid_config','reject');}await commerce.set(f.commerce);
   await offer('locked','locked');await unchangedReject('level','locked');
  });
  await t.test('exact balance and eight genuine concurrent requests against one offer',async()=>{
   await offer('race');const wallet=(await finance.get()).data();await finance.update({balance:20});
   const outcomes=await Promise.allSettled(Array.from({length:8},(_,i)=>buy(`race_${i}`,'race')));assert.equal(outcomes.filter(r=>r.status==='fulfilled').length,1);
   assert.equal((await finance.get()).data().balance,0);await offer('poor');await unchangedReject('insufficient','poor');await finance.set(wallet);
  });
  await t.test('eight distinct actual competing purchases cannot overspend one wallet',async()=>{
   const wallet=(await finance.get()).data();await finance.update({balance:40});for(let i=0;i<8;i++)await offer(`wallet_${i}`);
   const results=await Promise.allSettled(Array.from({length:8},(_,i)=>buy(`wallet_buy_${i}`,`wallet_${i}`)));assert.equal(results.filter(r=>r.status==='fulfilled').length,2);assert.equal((await finance.get()).data().balance,0);
   let count=0;for(let i=0;i<8;i++){const o=issued.get(`wallet_${i}`);if((await o.target.get()).exists){count++;assert.equal((await o.ref.get()).data().status,'consumed');}else assert.equal((await receipt(`wallet_buy_${i}`).get()).exists,false);}assert.equal(count,2);await finance.set(wallet);
  });
  await t.test('fault-injected rollback, SDK retry and post-commit response-loss recovery',async()=>{
   await offer('fault');receipt('rollback');const before=await snapshot([...refs.values()]);await assert.rejects(buy('rollback','fault',fault('rollback').db));assert.deepEqual(await snapshot([...refs.values()]),before);
   const retry=fault('retry');await buy('retry','fault',retry.db);assert.ok(retry.attempts()>=2);
   await offer('lost');const realCollection=db.collection.bind(db);let lose=true;
   const lossy=new Proxy(db,{get(target,key){if(key==='collection')return path=>{const c=realCollection(path);if(path!=='v2Players')return c;const doc=c.doc.bind(c);c.doc=uid=>{const p=doc(uid),sub=p.collection.bind(p);p.collection=name=>{const s=sub(name),d=s.doc.bind(s);s.doc=k=>{const ref=d(k),get=ref.get.bind(ref);ref.get=async(...args)=>{if(k==='lost_response'&&lose){lose=false;throw Object.assign(Error('synthetic lost response'),{code:14});}return get(...args);};return ref;};return s;};return p;};return c;};const v=target[key];return typeof v==='function'?v.bind(target):v;}});
   await assert.rejects(buy('lost_response','lost',lossy),e=>e.details.code==='RETRY_LATER');const wallet=(await finance.get()).data().balance;await buy('lost_response','lost');assert.equal((await finance.get()).data().balance,wallet);
  });
  await t.test('warehouse uniqueness, stale/skip upgrade, invalid capacity and cross-version compatibility',async()=>{
   await offer('duplicate_depot','open');await unchangedReject('duplicate_city_type','duplicate_depot');
   await offer('stale','upgrade');await unchangedReject('stale_upgrade','stale');
   const w=issued.get('depot').target,saved=(await w.get()).data();await w.update({level:1});await unchangedReject('corrupt_level','stale');await w.set(saved);
   await offer('third','upgrade3');const third=issued.get('third');await third.ref.update({expectedLevel:1});await unchangedReject('skip','third');await third.ref.set(third.raw);
   await w.update({usedCapacity:21});await unchangedReject('capacity','third');await w.set({...saved,status:'out_of_service'});await unchangedReject('warehouse_status','third');await w.set(saved);
   // A separately published immutable catalog version, not an in-place edit of the active catalog.
   const nextVersion=`next_${randomUUID()}`,nextLogistics={...f.logistics,version:nextVersion};
   const nextCommerce={...f.commerce,version:nextVersion,logisticsVersion:nextVersion,logisticsHash:digest(nextLogistics)};
   await own(`v2LogisticsCatalog/${nextVersion}`).set(nextLogistics);await own(`v2CommerceCatalog/${nextVersion}`).set(nextCommerce);
   await third.ref.update({catalogVersion:nextVersion,catalogHash:digest(nextCommerce)});await buy('upgrade_three','third');
   const dto=(await readMyWarehouseState(db,auth,{protocolVersion:1})).result.items[0];assert.equal(dto.level,3);assert.equal(dto.usedCapacity,'7');assert.equal(dto.capacityUnits,'30');assert.equal(dto.catalogVersion,nextVersion);
   await buy('buy_expand','expand');await buy('buy_depot','depot');
  });
  await t.test('purchased fleet supports real contract eligibility, assignment and delivery without acquisition stealing reservations',async()=>{
   const config={...f.config,templates:f.config.templates.map(t=>({...t,trailerCapability:'synthetic_vehicle'}))};await own(`v2ContractCatalog/${version}`).set(config);
   await own(`v2DeliveryRules/${version}`).set({schemaVersion:1,version,approval:'synthetic_emulator_only',contractConfigHash:digest(config),logisticsHash:digest(f.logistics),economyHash:digest(f.economy),routes:[{routeId:'synthetic_route',cargoId:'synthetic_cargo',durationMs:1000,xp:10}]});
   const assets=[{assetId:issued.get('vehicle').target.id,kind:'truck'},{assetId:issued.get('recruit').target.id,kind:'driver'},{assetId:issued.get('equipment').target.id,kind:'trailer'}];
   const epoch=Math.floor(now/600000);receipt('generate');own(`${root}/contractPools/epoch_${epoch}`);
   const generated=await generateContractOffers(db,auth,base('generate'),{configVersion:version,epoch,assets},clock);for(const id of generated.result.contractIds)own(`${root}/contracts/${id}`);
   const contractId=generated.result.contractIds[0];receipt('accept');await acceptContractCommand(db,auth,base('accept',{contractId}),clock);
   const id=deliveryIdFor(cid,contractId);own(`${root}/deliveries/${id}`);own(`${root}/assignments/${assignmentId(cid,id)}`);receipt('start');
   const started=await startDeliveryCommand(db,auth,base('start',{contractId,truckId:assets[0].assetId,driverId:assets[1].assetId,trailerId:assets[2].assetId}),clock);
   const reserved=[...assets.map(a=>own(`${root}/assets/${a.assetId}`)),own(`${root}/deliveries/${id}`),own(`${root}/contracts/${contractId}`)];const before=await snapshot(reserved);
   await offer('during_delivery');await buy('during_delivery','during_delivery');assert.deepEqual(await snapshot(reserved),before);await buy('buy_vehicle','vehicle');
   now=Date.parse(started.result.expectedArrivalAt);receipt('claim');own(`${root}/claims/delivery_${id}`);own(`${root}/ledger/${operationId(cid,`delivery_${id}`)}`);
   await claimDeliveryRewardCommand(db,auth,base('claim',{deliveryId:id}),clock);for(const a of assets)assert.equal((await own(`${root}/assets/${a.assetId}`).get()).data().assignmentId,null);
  });
  await t.test('bounded read-only offers, actual callable replay/auth/spoof and foreign company isolation',async()=>{
   const endpoint=name=>`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/${name}`;
   for(const name of ['buyVehicle','hireDriver','acquireTrailer','openWarehouse','upgradeWarehouse','getMyAcquisitionOffers'])assert.equal((await post(endpoint(name),{data:{protocolVersion:1}})).status,401);
   for(const [id,key] of [['vehicle','buy_vehicle'],['recruit','buy_recruit'],['equipment','buy_equipment'],['depot','buy_depot'],['expand','buy_expand']]){const r=await post(endpoint(issued.get(id).kind),{data:req(key,id)},token);assert.equal(r.status,200,JSON.stringify(r));}
   for(const extra of [{uid:'foreign'},{companyId:'foreign'},{limit:21},{after:'../other'},{status:'available'}])assert.equal((await post(endpoint('getMyAcquisitionOffers'),{data:{protocolVersion:1,...extra}},token)).status,400);
   assert.equal((await post(endpoint('buyVehicle'),{data:base('bad',{offerId:'poor',price:0})},token)).status,400);
   const before=await snapshot([...refs.values()]);let after,ids=[];do{const r=await readMyAcquisitionOffers(db,auth,{protocolVersion:1,limit:3,...(after?{after}:{})},clock);assert.ok(r.result.items.length<=3);ids.push(...r.result.items.map(o=>o.offerId));after=r.result.nextCursor;}while(after);
   assert.equal(ids.length,issued.size);assert.equal(new Set(ids).size,issued.size);assert.deepEqual(await snapshot([...refs.values()]),before);
   const read=await post(endpoint('getMyAcquisitionOffers'),{data:{protocolVersion:1}},token);assert.equal(read.status,200);assert.equal(typeof read.body.result.result.items[0].price,'string');
   const foreignUid=`synthetic_${randomUUID()}`,foreign={uid:foreignUid},foreignRoot=`v2Companies/${companyIdForUid(foreignUid)}`;own(`v2Players/${foreignUid}`);own(foreignRoot);own(`${foreignRoot}/state/finance`);own(`${foreignRoot}/state/progression`);own(`v2Players/${foreignUid}/commands/create`);own(`v2Players/${foreignUid}/commands/init`);
   await initializeIdentity(db,foreign,{protocolVersion:1});await createCompanyCommand(db,foreign,base('create',{name:'Foreign Synthetic'}));const foreignInit=await runEconomyCommand(db,foreign,base('init'),{kind:'initialize',id:version});own(`${foreignRoot}/claims/initialization`);own(`${foreignRoot}/ledger/${foreignInit.result.operationId}`);
   assert.deepEqual((await readMyAcquisitionOffers(db,foreign,{protocolVersion:1},clock)).result.items,[]);await assert.rejects(acquisitionCommand(db,foreign,req('foreign','vehicle'),'buyVehicle',clock));
  });
 }finally{if(token)await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:delete?key=synthetic-key',{idToken:token});for(const ref of refs.values())await ref.delete();await db.terminate();if(previous===undefined)delete process.env.FUNCTIONS_EMULATOR;else process.env.FUNCTIONS_EMULATOR=previous;}
});

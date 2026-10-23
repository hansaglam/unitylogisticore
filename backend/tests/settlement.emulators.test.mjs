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
const {generateContractOffers,acceptContractCommand,readMyContracts}=await import('../functions/lib/contracts.js');
const {startDeliveryCommand,claimDeliveryRewardCommand,readMyDeliveries}=await import('../functions/lib/deliveries.js');
const {deliveryIdFor}=await import('../functions/lib/deliveryModel.js');
const {digest}=await import('../functions/lib/logisticsModel.js');
const {settlementFixtures}=await import('./settlement.fixtures.mjs');
const {candidates}=await import('./contracts.fixtures.mjs');
const request=(id,payload={})=>({protocolVersion:1,idempotencyKey:id,payload});
async function post(url,data,token){const r=await fetch(url,{method:'POST',redirect:'error',headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:JSON.stringify(data),signal:AbortSignal.timeout(20000)});return {status:r.status,body:await r.json()};}
const endpoint=name=>`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/${name}`;
test('settlement-v2: pinned costs, real atomic net claims and legacy compatibility',{timeout:240000},async t=>{
 const previous=process.env.FUNCTIONS_EMULATOR;process.env.FUNCTIONS_EMULATOR='true';const db=createLocalFirestore(),refs=new Map(),tokens=[];
 const own=path=>{const r=db.doc(path);refs.set(path,r);return r;};
 const snapshot=async list=>(await db.getAll(...list)).map(d=>({path:d.ref.path,data:d.data(),time:d.updateTime}));
 const fault=mode=>{let count=0;return {get attempts(){return count;},db:new Proxy(db,{get(target,key){if(key==='runTransaction')return cb=>target.runTransaction(async tx=>{const result=await cb(tx);count++;if(mode==='rollback')throw Error('controlled rollback');if(count===1)throw Object.assign(Error('controlled SDK retry'),{code:10});return result;});const value=target[key];return typeof value==='function'?value.bind(target):value;}})};};
 async function setup(options={}){
  const signed=await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=synthetic-key',{returnSecureToken:true});assert.equal(signed.status,200);
  const uid=signed.body.localId,token=signed.body.idToken;tokens.push(token);const auth={uid},cid=companyIdForUid(uid),root=`v2Companies/${cid}`,version=`synthetic_${randomUUID()}`;
  const f=settlementFixtures(version,options);if(options.legacy){f.config.schemaVersion=1;delete f.config.costCatalogVersion;delete f.config.costCatalogHash;f.rules.contractConfigHash=digest(f.config);}
  const costRef=own(`v2OperatingCostCatalog/${version}`),configRef=own(`v2ContractCatalog/${version}`),rulesRef=own(`v2DeliveryRules/${version}`);
  for(const [path,data] of [[`v2WorldConfig/${version}`,f.economy],[`v2LogisticsCatalog/${version}`,f.logistics]])await own(path).set(data);
  await configRef.set(f.config);await rulesRef.set(f.rules);if(!options.legacy)await costRef.set(f.costCatalog);
  own(`v2Players/${uid}`);own(root);const receipt=key=>own(`v2Players/${uid}/commands/${key}`);
  await initializeIdentity(db,auth,{protocolVersion:1});receipt('create');await createCompanyCommand(db,auth,request('create',{name:'Synthetic Net Settlement'}));
  receipt('init');const initialized=await runEconomyCommand(db,auth,request('init'),{kind:'initialize',id:version});
  own(`${root}/ledger/${initialized.result.operationId}`);own(`${root}/claims/initialization`);
  const finance=own(`${root}/state/finance`),progression=own(`${root}/state/progression`),truck=own(`${root}/assets/starter_truck`),driver=own(`${root}/assets/starter_driver`);
  own(`${root}/provisioning/starterFleet`);receipt('starter');await provisionStarterFleet(db,auth,request('starter'),version);
  const ctx={...f,auth,uid,token,cid,root,version,costRef,configRef,rulesRef,receipt,finance,progression,truck,driver,now:Math.floor(Date.now()/600000)*600000+1000};
  ctx.clock=()=>ctx.now;
  ctx.generate=async(key='generate',assets=candidates)=>{receipt(key);const epoch=Math.floor(ctx.now/600000);own(`${root}/contractPools/epoch_${epoch}`);
   const result=await generateContractOffers(db,auth,request(key),{configVersion:version,epoch,assets},ctx.clock);
   return result.result.contractIds.map(id=>own(`${root}/contracts/${id}`));};
  ctx.accept=async(ref,key)=>{receipt(key);return acceptContractCommand(db,auth,request(key,{contractId:ref.id}),ctx.clock);};
  ctx.track=id=>{const source=`delivery_${id}`,op=operationId(cid,source);return {delivery:own(`${root}/deliveries/${id}`),assignment:own(`${root}/assignments/${assignmentId(cid,id)}`),claim:own(`${root}/claims/${source}`),ledger:own(`${root}/ledger/${op}`),journal:own(`${root}/settlementJournals/${op}`)};};
  ctx.start=async(ref,key='start',assets=candidates,database=db)=>{receipt(key);ctx.track(deliveryIdFor(cid,ref.id));return startDeliveryCommand(database,auth,request(key,{contractId:ref.id,truckId:assets.find(a=>a.kind==='truck').assetId,driverId:assets.find(a=>a.kind==='driver').assetId}),ctx.clock);};
  ctx.claim=(id,key='claim',database=db)=>{receipt(key);ctx.track(id);return claimDeliveryRewardCommand(database,auth,request(key,{deliveryId:id}),ctx.clock);};
  return ctx;
 }
 try{
  const f=await setup();let contracts,accepted,started,id,paid,winner;
  await t.test('missing/unapproved/drifted/mismatched catalog and invalid amounts reject offers without partial pool',async()=>{
   const original=f.costCatalog;
   const variants=[null,{...original,approval:'production'},{...original,economyHash:'0'.repeat(64)},{...original,logisticsVersion:'unknown'},
    {...original,currency:'LGC'},{...original,scale:3},...[-1,0.5,Number.MAX_SAFE_INTEGER+1].map(n=>({...original,entries:original.entries.map(e=>({...e,costs:{...e.costs,fuelCost:n}}))})),
    {...original,entries:original.entries.map(e=>({...e,costs:{fuelCost:Number.MAX_SAFE_INTEGER,driverCost:1,maintenanceCost:0,tollCost:0}})),maxCost:Number.MAX_SAFE_INTEGER},
    {...original,entries:original.entries.map(e=>({...e,costs:{...e.costs,fuelCost:2}}))}];
   for(let i=0;i<variants.length;i++){if(variants[i])await f.costRef.set(variants[i]);else await f.costRef.delete();await assert.rejects(f.generate(`invalid_${i}`));assert.equal((await f.receipt(`invalid_${i}`).get()).exists,false);}
   await f.costRef.set(original);assert.equal((await db.collection(`${f.root}/contracts`).get()).size,0);
  });
  await t.test('equal or greater costs rejected during authoritative preparation; no offer is repriced',async()=>{
   for(const total of [6,7]){
    const catalog={...f.costCatalog,entries:f.costCatalog.entries.map(e=>({...e,costs:{fuelCost:total,driverCost:0,maintenanceCost:0,tollCost:0}}))};
    await f.costRef.set(catalog);await f.configRef.set({...f.config,costCatalogHash:digest(catalog)});await assert.rejects(f.generate(`nonpositive_${total}`));
    assert.equal((await db.collection(`${f.root}/contracts`).get()).size,0);
   }await f.costRef.set(f.costCatalog);await f.configRef.set(f.config);
  });
  await t.test('v2 offer arithmetic and pool integrity; acceptance has no financial effect',async()=>{
   contracts=await f.generate();const raw=(await contracts[0].get()).data();assert.equal(raw.schemaVersion,2);assert.equal(raw.terms.settlement.totalOperatingCosts,4);assert.equal(raw.terms.settlement.netReward,2);
   const before=await snapshot([f.finance,f.progression]);const pool=own(`${f.root}/contractPools/epoch_${raw.epoch}`),saved=(await pool.get()).data();
   await pool.update({termsHashes:['0'.repeat(64),...saved.termsHashes.slice(1)]});await assert.rejects(f.accept(contracts[0],'pool_corrupt'));await pool.set(saved);
   accepted=await f.accept(contracts[0],'accept');await f.accept(contracts[1],'accept2');assert.deepEqual(await snapshot([f.finance,f.progression]),before);
   assert.equal(accepted.result.reward,'6');assert.equal(accepted.result.settlement.netReward,'2');
  });
  await t.test('accepted quote mutation and catalog drift fail start and read without repricing',async()=>{
   const raw=(await contracts[0].get()).data();await contracts[0].update({'terms.settlement.netReward':3});await assert.rejects(f.start(contracts[0],'corrupt_quote'));await assert.rejects(readMyContracts(db,f.auth,{protocolVersion:1},f.clock));await contracts[0].set(raw);
   await f.costRef.update({policy:'changed'});await assert.rejects(f.start(contracts[0],'cost_drift'));await assert.rejects(readMyContracts(db,f.auth,{protocolVersion:1},f.clock));await f.costRef.set(f.costCatalog);
  });
  await t.test('zero-balance start reserves assets, creates no financial claim and pins quote',async()=>{
   await f.finance.update({balance:0});const before=await snapshot([f.finance,f.progression]);started=await f.start(contracts[0]);id=started.result.deliveryId;
   const tracked=f.track(id);assert.deepEqual(await snapshot([f.finance,f.progression]),before);assert.equal((await f.truck.get()).data().assignmentId,assignmentId(f.cid,id));
   assert.equal((await tracked.delivery.get()).data().schemaVersion,2);assert.deepEqual((await tracked.delivery.get()).data().settlement,(await contracts[0].get()).data().terms.settlement);
   for(const key of ['claim','ledger','journal'])assert.equal((await tracked[key].get()).exists,false);
  });
  await t.test('arrival, cost drift, delivery snapshot, stale assignment and wallet/XP overflow reject atomically',async()=>{
   const arrival=Date.parse(started.result.expectedArrivalAt);f.now=arrival-1;await assert.rejects(f.claim(id,'early'),e=>e.details.code==='DELIVERY_NOT_ARRIVED');f.now=arrival;
   const tr=f.track(id),critical=[f.finance,f.progression,f.truck,f.driver,contracts[0],...Object.values(tr)];
   const probes=[ [f.finance,{balance:f.economy.maxBalance}],[f.progression,{xp:f.economy.maxXp,level:3}],
    [f.truck,{assignmentId:'0'.repeat(64)}],[tr.delivery,{'settlement.fuelCost':0}],[f.costRef,{approval:'unapproved'}] ];
   for(let i=0;i<probes.length;i++){const [ref,patch]=probes[i],saved=(await ref.get()).data();await ref.update(patch);const before=await snapshot(critical);await assert.rejects(f.claim(id,`blocked_${i}`));assert.deepEqual(await snapshot(critical),before);await ref.set(saved);}
  });
  await t.test('existing journal/claim/ledger independently block payout; queued-write fault rolls back everything',async()=>{
   const tr=f.track(id),critical=[f.finance,f.progression,f.truck,f.driver,contracts[0],...Object.values(tr)];
   for(const name of ['journal','claim','ledger']){await tr[name].set({synthetic:true});const before=await snapshot(critical);await assert.rejects(f.claim(id,`exists_${name}`));assert.deepEqual(await snapshot(critical),before);await tr[name].delete();}
   const before=await snapshot(critical);await assert.rejects(f.claim(id,'rollback',fault('rollback').db));assert.deepEqual(await snapshot(critical),before);assert.equal((await f.receipt('rollback').get()).exists,false);
  });
  await t.test('eight genuine Firestore transaction claims produce exactly one net credit and immutable journal',async()=>{
   const results=await Promise.allSettled(Array.from({length:8},(_,i)=>f.claim(id,`race_${i}`)));assert.equal(results.filter(r=>r.status==='fulfilled').length,1);winner=results.findIndex(r=>r.status==='fulfilled');paid=results[winner].value;
   assert.equal(paid.result.reward,'6');assert.equal(paid.result.settlement.netReward,'2');assert.equal((await f.finance.get()).data().balance,2);
   const tr=f.track(id),journal=(await tr.journal.get()).data(),ledger=(await tr.ledger.get()).data(),claim=(await tr.claim.get()).data();
   assert.equal(journal.quote.grossReward-journal.quote.totalOperatingCosts,ledger.delta);assert.equal(ledger.delta,2);assert.equal(ledger.reason,'delivery_net_reward');assert.equal(ledger.schemaVersion,1);
   assert.equal(journal.previousBalance+ledger.delta,ledger.balanceAfter);assert.equal(journal.balanceAfter,2);assert.equal(claim.operationId,ledger.operationId);assert.equal(journal.operationId,ledger.operationId);
   assert.equal((await f.progression.get()).data().xp,10);assert.equal((await f.progression.get()).data().level,2);assert.equal((await f.truck.get()).data().assignmentId,null);
   assert.equal((await tr.assignment.get()).data().status,'released');assert.equal((await contracts[0].get()).data().status,'completed');
  });
  await t.test('historical result/time replay survives new schema; different intent/key and stale release reject',async()=>{
   assert.deepEqual(await f.claim(id,`race_${winner}`),paid);await assert.rejects(f.claim(id,'duplicate'));
   assert.deepEqual(await f.accept(contracts[0],'accept'),accepted);assert.deepEqual(await f.start(contracts[0]),started);
   await assert.rejects(f.claim(deliveryIdFor(f.cid,contracts[1].id),`race_${winner}`),e=>e.details.code==='IDEMPOTENCY_CONFLICT');
   const second=await f.start(contracts[1],'second');const before=await snapshot([f.truck,f.driver,f.finance]);await assert.rejects(f.claim(id,'stale'));assert.deepEqual(await snapshot([f.truck,f.driver,f.finance]),before);
   f.second=second.result.deliveryId;f.now=Date.parse(second.result.expectedArrivalAt);
  });
  await t.test('SDK aborted retry reruns real transaction and post-commit response failure recovers unchanged',async()=>{
   const retry=fault('retry');let lose=true;
   const lossy=new Proxy(retry.db,{get(target,key){if(key==='collection')return path=>{const c=db.collection(path);if(path!=='v2Players')return c;const doc=c.doc.bind(c);c.doc=uid=>{const p=doc(uid),sub=p.collection.bind(p);p.collection=name=>{const s=sub(name),d=s.doc.bind(s);s.doc=k=>{const ref=d(k),get=ref.get.bind(ref);ref.get=async(...args)=>{if(k==='lost'&&lose){lose=false;throw Object.assign(Error('controlled post-commit read failure'),{code:14});}return get(...args);};return ref;};return s;};return p;};return c;};const v=target[key];return typeof v==='function'?v.bind(target):v;}});
   await assert.rejects(f.claim(f.second,'lost',lossy),e=>e.details.code==='RETRY_LATER');assert.ok(retry.attempts>=2);
   const receipt=(await f.receipt('lost').get()).data(),recovered=await f.claim(f.second,'lost');assert.deepEqual(recovered.result,receipt.result);assert.equal(recovered.serverTime,receipt.committedAt.toDate().toISOString());assert.equal((await f.finance.get()).data().balance,4);
  });
  await t.test('Auth/Functions v2 DTOs and replay are bounded/read-only and financial intent spoofing denied',async()=>{
   const before=await snapshot([...refs.values()]);
   for(const name of ['getMyContracts','getMyDeliveries']){
    assert.equal((await post(endpoint(name),{data:{protocolVersion:1}})).status,401);
    const response=await post(endpoint(name),{data:{protocolVersion:1,limit:1}},f.token);assert.equal(response.status,200);assert.equal(response.body.result.result.items.length,1);
    assert.equal(response.body.result.result.items[0].settlement.netReward,'2');
    assert.equal((await post(endpoint(name),{data:{protocolVersion:1,limit:21}},f.token)).status,400);
   }
   const replay=await post(endpoint('claimDeliveryReward'),{data:request('lost',{deliveryId:f.second})},f.token);assert.equal(replay.status,200);assert.equal(replay.body.result.result.balance,'4');
   for(const key of ['reward','fuelCost','netReward','companyId','xp'])assert.equal((await post(endpoint('claimDeliveryReward'),{data:request('spoof',{deliveryId:id,[key]:1})},f.token)).status,400);
   assert.deepEqual(await snapshot([...refs.values()]),before);
   const journal=f.track(id).journal,saved=(await journal.get()).data();await journal.update({balanceAfter:999});await assert.rejects(readMyDeliveries(db,f.auth,{protocolVersion:1},f.clock));await journal.set(saved);
  });
  await t.test('legacy v1 zero-cost gross-only claim and historical accept/start/claim receipts remain exact',async()=>{
   const old=await setup({legacy:true}),pool=await old.generate(),a=await old.accept(pool[0],'accept'),s=await old.start(pool[0]);old.now=Date.parse(s.result.expectedArrivalAt);
   const p=await old.claim(s.result.deliveryId);assert.equal(p.result.balance,'106');assert.equal(Object.hasOwn(p.result,'settlement'),false);assert.equal((await old.track(s.result.deliveryId).journal.get()).exists,false);
   assert.deepEqual(await old.accept(pool[0],'accept'),a);assert.deepEqual(await old.start(pool[0]),s);assert.deepEqual(await old.claim(s.result.deliveryId),p);
   assert.equal((await readMyDeliveries(db,old.auth,{protocolVersion:1},old.clock)).result.items[0].reward,'6');
   await assert.rejects(claimDeliveryRewardCommand(db,old.auth,request('cross',{deliveryId:id}),old.clock));
   assert.equal((await post(endpoint('claimDeliveryReward'),{data:request('cross',{deliveryId:id})},old.token)).status,400);
  });
  await t.test('minimal positive net can credit exact wallet maximum through actual Functions claim',async()=>{
   const min=await setup({gross:5}),pool=await min.generate();await min.accept(pool[0],'accept');await min.finance.update({balance:min.economy.maxBalance-1});const s=await min.start(pool[0]);
   min.receipt('api_claim');const response=await post(endpoint('claimDeliveryReward'),{data:request('api_claim',{deliveryId:s.result.deliveryId})},min.token);
   assert.equal(response.status,200);assert.equal(response.body.result.result.settlement.netReward,'1');assert.equal((await min.finance.get()).data().balance,min.economy.maxBalance);
  });
  await t.test('two genuine concurrent deliveries preserve shared wallet updates; accepted expiry does not block claim',async()=>{
   const alternative=[];for(const kind of ['truck','driver']){const assetId=`other_${kind}`;alternative.push({assetId,kind});await own(`${f.root}/assets/${assetId}`).set({schemaVersion:1,assetId,kind,companyId:f.cid,catalogId:`test_${kind}`,catalogVersion:f.version,catalogHash:digest(f.logistics),status:'operational',assignmentId:null,acquiredAt:Timestamp.now()});}
   f.now+=600000;const a=await f.generate('pool_a');await f.accept(a[0],'accept_a');const sa=await f.start(a[0],'start_a');
   f.now+=600000;const b=await f.generate('pool_b',alternative);await f.accept(b[0],'accept_b');const sb=await f.start(b[0],'start_b',alternative);
   f.now+=600000;const before=(await f.finance.get()).data().balance;
   const results=await Promise.all([f.claim(sa.result.deliveryId,'claim_a'),f.claim(sb.result.deliveryId,'claim_b')]);assert.equal(results.length,2);assert.equal((await f.finance.get()).data().balance,before+4);
   const entries=await Promise.all(results.map(r=>own(`${f.root}/settlementJournals/${r.result.operationId}`).get()));assert.deepEqual(entries.map(d=>d.data().balanceAfter).sort((x,y)=>x-y),[before+2,before+4]);
  });
 }finally{
  for(const ref of refs.values())await ref.delete();
  for(const token of tokens)assert.equal((await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:delete?key=synthetic-key',{idToken:token})).status,200);
  await db.terminate();if(previous===undefined)delete process.env.FUNCTIONS_EMULATOR;else process.env.FUNCTIONS_EMULATOR=previous;
 }
});

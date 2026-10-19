import test from 'node:test';
import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {assertLocalEnvironment,LOCAL_PROJECT} from '../functions/lib/localEnvironment.js';
assertLocalEnvironment(process.env);
const {createLocalFirestore,initializeIdentity}=await import('../functions/lib/identity.js');
const {companyIdForUid,createCompanyCommand}=await import('../functions/lib/company.js');
const {runEconomyCommand}=await import('../functions/lib/economy.js');
const {provisionStarterFleet}=await import('../functions/lib/fleet.js');
const {generateContractOffers,acceptContractCommand,readMyContracts}=await import('../functions/lib/contracts.js');
const {contractIdFor,epochFor}=await import('../functions/lib/contractModel.js');
const {contractFixtures,candidates}=await import('./contracts.fixtures.mjs');
const req=(key,payload={})=>({protocolVersion:1,idempotencyKey:key,payload});
async function post(url,body,token){const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:JSON.stringify(body),signal:AbortSignal.timeout(15000)});return {status:r.status,body:await r.json()};}
test('contracts: deterministic pools, eligibility, concurrent acceptance, time and read isolation',{timeout:240000},async t=>{
 const previous=process.env.FUNCTIONS_EMULATOR;process.env.FUNCTIONS_EMULATOR='true';const db=createLocalFirestore();const refs=new Map();
 const own=path=>{const ref=db.doc(path);refs.set(path,ref);return ref;};let token;
 try{
  const signup=await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=synthetic-key',{returnSecureToken:true});assert.equal(signup.status,200);token=signup.body.idToken;
  const uid=signup.body.localId,auth={uid},cid=companyIdForUid(uid),root=`v2Companies/${cid}`,version=`synthetic_${randomUUID()}`,f=contractFixtures(version);
  const player=own(`v2Players/${uid}`),company=own(root),finance=own(`${root}/state/finance`),progression=own(`${root}/state/progression`);
  const truck=own(`${root}/assets/starter_truck`),driver=own(`${root}/assets/starter_driver`);own(`${root}/provisioning/starterFleet`);
  const config=own(`v2ContractCatalog/${version}`),logistics=own(`v2LogisticsCatalog/${version}`),economy=own(`v2WorldConfig/${version}`);
  const receipt=key=>own(`v2Players/${uid}/commands/${key}`);
  let now=epochFor(Date.now(),600000)*600000+1000;const epoch=epochFor(now,600000);const clock=()=>now;
  const plan={configVersion:version,epoch,assets:candidates};
  const trackPool=e=>{own(`${root}/contractPools/epoch_${e}`);return [0,1,2].map(i=>own(`${root}/contracts/${contractIdFor(cid,e,i)}`));};
  const offers=trackPool(epoch);
  const generate=(key,p=plan,database=db)=>{receipt(key);trackPool(p.epoch);return generateContractOffers(database,auth,req(key),p,clock);};
  const accept=(key,id=offers[0].id,database=db)=>{receipt(key);return acceptContractCommand(database,auth,req(key,{contractId:id}),clock);};
  const snapshot=async list=>(await db.getAll(...list)).map(d=>({data:d.data(),time:d.updateTime?.toMillis()}));
  await initializeIdentity(db,auth,{protocolVersion:1});receipt('create');await createCompanyCommand(db,auth,req('create',{name:'Synthetic Contracts'}));
  await economy.set(f.economy);await logistics.set(f.logistics);await config.set(f.config);
  await t.test('pending, missing identity, missing starter and invalid configs fail without pools',async()=>{
   await assert.rejects(generate('pending'));const identity=(await player.get()).data();await player.update({identityStatus:'suspended'});await assert.rejects(generate('suspended'));await player.delete();await assert.rejects(generate('no_identity'));await player.set(identity);
   receipt('init');const init=await runEconomyCommand(db,auth,req('init'),{kind:'initialize',id:version});own(`${root}/ledger/${init.result.operationId}`);own(`${root}/claims/initialization`);
   await assert.rejects(generate('no_starter'));receipt('starter');await provisionStarterFleet(db,auth,req('starter'),version);
   for(const patch of [{approval:'unapproved'},{logisticsVersion:'unknown'},{routes:[{...f.config.routes[0],destinationCityId:'istanbul'}]},
    {templates:[{...f.config.templates[0],truckCapability:'unsupported'}]}, {templates:[{...f.config.templates[0],quantity:11}]},
    {templates:[{...f.config.templates[0],trailerCapability:'synthetic_vehicle'}]}]){
    await config.set({...f.config,...patch});await assert.rejects(generate('bad_config'));assert.equal((await receipt('bad_config').get()).exists,false);
   }await config.set(f.config);assert.equal((await offers[0].get()).exists,false);
  });
  await t.test('rollback after queued pool writes and real SDK ABORTED retry are atomic',async()=>{
   const rollback=new Proxy(db,{get(target,key){if(key==='runTransaction')return callback=>target.runTransaction(async tx=>{await callback(tx);throw Error('synthetic rollback');});const v=target[key];return typeof v==='function'?v.bind(target):v;}});
   await assert.rejects(generate('rollback',plan,rollback));for(const ref of offers)assert.equal((await ref.get()).exists,false);assert.equal((await receipt('rollback').get()).exists,false);
   let attempts=0;const retry=new Proxy(db,{get(target,key){if(key==='runTransaction')return callback=>target.runTransaction(async tx=>{const result=await callback(tx);if(++attempts===1)throw Object.assign(Error('synthetic aborted'),{code:10});return result;});const v=target[key];return typeof v==='function'?v.bind(target):v;}});
   const e=epoch-1;const previousNow=now;now=e*600000+1000;const result=await generate('retry',{...plan,epoch:e},retry);assert.ok(attempts>=2);assert.equal(result.result.contractIds.length,3);now=previousNow;
  });
  let generated;
  await t.test('eight generation attempts produce one bounded pool, stable replay and no refresh writes',async()=>{
   const results=await Promise.allSettled(Array.from({length:8},(_,i)=>generate(`gen_${i}`)));assert.equal(results.filter(r=>r.status==='fulfilled').length,1);
   const index=results.findIndex(r=>r.status==='fulfilled');generated=results[index].value;assert.deepEqual(await generate(`gen_${index}`),generated);await assert.rejects(generate('duplicate'));
   const before=await snapshot([...refs.values()]);await readMyContracts(db,auth,{protocolVersion:1},clock);await readMyContracts(db,auth,{protocolVersion:1},clock);assert.deepEqual(await snapshot([...refs.values()]),before);
   const original=(await offers[1].get()).data();await offers[1].delete();await assert.rejects(generate(`gen_${index}`));await offers[1].set(original);
   const marker=own(`${root}/contractPools/epoch_${epoch}`),savedMarker=(await marker.get()).data();await marker.delete();await assert.rejects(generate(`gen_${index}`));await marker.set(savedMarker);
  });
  await t.test('current ownership, migration, equipment and strict intents prevent unauthorized acceptance',async()=>{
   const original=(await company.get()).data();await company.update({ownerUid:'foreign'});await assert.rejects(accept('foreign'));await company.set(original);
   const legacy=own(`users/${uid}`);await legacy.set({synthetic:true,money:1});const before=await snapshot([legacy]);await assert.rejects(accept('legacy'));assert.deepEqual(await snapshot([legacy]),before);await legacy.delete();
   const migration=own(`v2MigrationRecords/${uid}`);await migration.set({status:'pending'});await assert.rejects(accept('migration'));await migration.delete();
   await truck.update({assignmentId:'a'.repeat(64)});await assert.rejects(accept('busy'));await truck.update({assignmentId:null});
   const saved=(await offers[0].get()).data();await offers[0].update({companyId:'foreign'});await assert.rejects(accept('scope'));await offers[0].set(saved);
   await assert.rejects(accept('unknown','unknown'));await assert.rejects(acceptContractCommand(db,undefined,req('unauth',{contractId:offers[0].id}),clock));
   for(const payload of [{contractId:offers[0].id,reward:999},{contractId:offers[0].id,routeId:'spoof'},{contractId:offers[0].id,uid:'other'}])await assert.rejects(acceptContractCommand(db,auth,req('spoof',payload),clock));
  });
  let accepted;
  await t.test('eight accepts have one winner; receipts replay after future status and config changes without repricing',async()=>{
   const before=await snapshot([finance,progression,truck,driver]);const results=await Promise.allSettled(Array.from({length:8},(_,i)=>accept(`accept_${i}`)));
   assert.equal(results.filter(r=>r.status==='fulfilled').length,1);const index=results.findIndex(r=>r.status==='fulfilled');accepted=results[index].value;
   const httpReplay=await post(`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/acceptContract`,{data:req(`accept_${index}`,{contractId:offers[0].id})},token);assert.equal(httpReplay.status,200);assert.deepEqual(httpReplay.body.result,accepted);
   assert.deepEqual(await accept(`accept_${index}`),accepted);await assert.rejects(accept(`accept_${index}`,offers[1].id),e=>e.details.code==='IDEMPOTENCY_CONFLICT');await assert.rejects(accept('different'));
   assert.deepEqual(await snapshot([finance,progression,truck,driver]),before);assert.equal(accepted.result.equipmentReserved,false);
   await offers[0].update({status:'in_progress'});await config.set({...f.config,templates:f.config.templates.map(t=>({...t,rewardPerUnit:7}))});
   assert.deepEqual(await accept(`accept_${index}`),accepted);const read=await readMyContracts(db,auth,{protocolVersion:1},clock);assert.equal(read.result.items.find(c=>c.contractId===offers[0].id).reward,'6');await config.set(f.config);
  });
  await t.test('trusted exact expiration boundary, quota and lost-response recovery retain one effect',async()=>{
   now=(epoch+1)*600000;await assert.rejects(accept('expired',offers[1].id),e=>e.details.code==='OFFER_EXPIRED');assert.equal((await receipt('expired').get()).exists,false);
   now=(epoch+1)*600000-1;
   // Fail the post-commit receipt get once; transaction reads still use the real DocumentReference.
   const realCollection=db.collection.bind(db);let lose=true;
   const lossy=new Proxy(db,{get(target,key){if(key==='collection')return path=>{
    const collection=realCollection(path);if(path!=='v2Players')return collection;
    const realDoc=collection.doc.bind(collection);collection.doc=id=>{const playerRef=realDoc(id);const realSub=playerRef.collection.bind(playerRef);playerRef.collection=sub=>{
     const commands=realSub(sub);const commandDoc=commands.doc.bind(commands);commands.doc=k=>{const ref=commandDoc(k);const get=ref.get.bind(ref);ref.get=async(...args)=>{if(k==='lost'&&lose){lose=false;throw Object.assign(Error('lost'),{code:14});}return get(...args);};return ref;};return commands;};return playerRef;};return collection;
   };const v=target[key];return typeof v==='function'?v.bind(target):v;}});
   await assert.rejects(accept('lost',offers[1].id,lossy),e=>e.details.code==='RETRY_LATER');const recovered=await accept('lost',offers[1].id);assert.equal(recovered.result.reward,'6');
   await assert.rejects(accept('quota',offers[2].id),e=>e.details.code==='CONTRACT_QUOTA_REACHED');assert.equal((await receipt('quota').get()).exists,false);
   now=(epoch+1)*600000+1;const read=await readMyContracts(db,auth,{protocolVersion:1},clock);assert.equal(read.result.items.find(c=>c.contractId===offers[1].id).expired,false);
  });
  await t.test('Auth/Functions API, pagination, safe DTOs and cross-company isolation',async()=>{
   const endpoint=name=>`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/${name}`;
   assert.equal((await post(endpoint('getMyContracts'),{data:{protocolVersion:1}})).status,401);
   for(const extra of [{uid:'foreign'},{companyId:'foreign'},{limit:21},{after:'../other'},{status:'offered'}])assert.equal((await post(endpoint('getMyContracts'),{data:{protocolVersion:1,...extra}},token)).status,400);
   const before=await snapshot([...refs.values()]);let after,ids=[];
   do{const read=await readMyContracts(db,auth,{protocolVersion:1,limit:2,...(after?{after}:{})},clock);assert.ok(read.result.items.length<=2);ids.push(...read.result.items.map(c=>c.contractId));after=read.result.nextCursor;}while(after);
   assert.equal(ids.length,6);assert.equal(new Set(ids).size,6);
   const http=await post(endpoint('getMyContracts'),{data:{protocolVersion:1}},token);assert.equal(http.status,200);assert.equal(typeof http.body.result.result.items[0].reward,'string');assert.deepEqual(await snapshot([...refs.values()]),before);
   const otherUid=`synthetic_other_${randomUUID()}`,otherAuth={uid:otherUid};own(`v2Players/${otherUid}`);const otherCid=companyIdForUid(otherUid);own(`v2Companies/${otherCid}`);own(`v2Players/${otherUid}/commands/create`);
   await initializeIdentity(db,otherAuth,{protocolVersion:1});await createCompanyCommand(db,otherAuth,req('create',{name:'Other Synthetic'}));
   assert.deepEqual((await readMyContracts(db,otherAuth,{protocolVersion:1},clock)).result.items,[]);await assert.rejects(acceptContractCommand(db,otherAuth,req('foreign',{contractId:offers[0].id}),clock));
   const response=await post(endpoint('acceptContract'),{data:req('http_unknown',{contractId:'unknown'})},token);assert.equal(response.status,400);
  });
 }finally{
  if(token)await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:delete?key=synthetic-key',{idToken:token});
  for(const ref of refs.values())await ref.delete();await db.terminate();if(previous===undefined)delete process.env.FUNCTIONS_EMULATOR;else process.env.FUNCTIONS_EMULATOR=previous;
 }
});

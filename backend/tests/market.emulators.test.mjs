import test from 'node:test';
import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {assertLocalEnvironment,LOCAL_PROJECT} from '../functions/lib/localEnvironment.js';
assertLocalEnvironment(process.env);
const {Timestamp}=await import('firebase-admin/firestore');
const {createLocalFirestore,initializeIdentity}=await import('../functions/lib/identity.js');
const {companyIdForUid,createCompanyCommand}=await import('../functions/lib/company.js');
const {runEconomyCommand,operationId}=await import('../functions/lib/economy.js');
const {provisionStarterFleet}=await import('../functions/lib/fleet.js');
const {generateContractOffers,acceptContractCommand,readMyContracts}=await import('../functions/lib/contracts.js');
const {startDeliveryCommand,claimDeliveryRewardCommand,readMyDeliveries}=await import('../functions/lib/deliveries.js');
const {initializeBank,bankCommand}=await import('../functions/lib/bank.js');
const {readMyMarketState}=await import('../functions/lib/market.js');
const {marketQuote,marketEpoch}=await import('../functions/lib/marketModel.js');
const {digest}=await import('../functions/lib/logisticsModel.js');
const {marketFixtures}=await import('./market.fixtures.mjs');
const {candidates}=await import('./contracts.fixtures.mjs');
const req=(key,payload={})=>({protocolVersion:1,idempotencyKey:key,payload});
async function post(url,data,token){const r=await fetch(url,{method:'POST',redirect:'error',headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:JSON.stringify(data),signal:AbortSignal.timeout(60000)});return {status:r.status,body:await r.json()};}
test('BACKEND-09B: real regional market authority, pinned V4 and atomic V3 settlement',{timeout:360000},async t=>{
 const previous=process.env.FUNCTIONS_EMULATOR;process.env.FUNCTIONS_EMULATOR='true';const db=createLocalFirestore(),roots=[],configs=[],tokens=[];
 async function setup(options={}){
  const sign=await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=synthetic-key',{returnSecureToken:true});assert.equal(sign.status,200);
  const uid=sign.body.localId,token=sign.body.idToken;tokens.push(token);const auth={uid},cid=companyIdForUid(uid),root=db.doc(`v2Companies/${cid}`),player=db.doc(`v2Players/${uid}`);roots.push(root,player);
  const version=options.shared?.version??`market_${randomUUID()}`,f=options.shared??marketFixtures(version,options);
  if(!options.shared){for(const [collection,value] of Object.entries({v2WorldConfig:f.economy,v2LogisticsCatalog:f.logistics,v2OperatingCostCatalog:f.costCatalog,v2ContractCatalog:f.config,v2DeliveryRules:f.rules,v2BankCatalog:f.bank,v2MarketCatalog:f.market})){
   const ref=db.collection(collection).doc(version);configs.push(ref);await ref.set(value);
  }const selector=db.collection('v2MarketSelection').doc(version);configs.push(selector);await selector.set({schemaVersion:1,approval:'synthetic_emulator_only',contractVersion:version,contractHash:digest(f.config)});}
  await initializeIdentity(db,auth,{protocolVersion:1});await createCompanyCommand(db,auth,req('create',{name:'Synthetic Market Company'}));
  await runEconomyCommand(db,auth,req('economy'),{kind:'initialize',id:version});
  if(!options.pending){await provisionStarterFleet(db,auth,req('starter'),version);await initializeBank(db,auth,req('bank'),version);}
  const x={...f,version,uid,auth,token,cid,root,player,now:Math.floor(Date.now()/600000)*600000+1000,finance:root.collection('state').doc('finance'),progression:root.collection('state').doc('progression'),catalog:db.collection('v2MarketCatalog').doc(version),selector:db.collection('v2MarketSelection').doc(version)};
  x.clock=()=>x.now;x.read=()=>readMyMarketState(db,auth,{protocolVersion:1},x.clock);
  x.generate=(key='generate',assets=candidates,database=db,epoch=Math.floor(x.now/600000))=>generateContractOffers(database,auth,req(key),{configVersion:version,epoch,assets},x.clock);
  x.accept=(id,key='accept')=>acceptContractCommand(db,auth,req(key,{contractId:id}),x.clock);
  x.start=(id,key='start',assets=candidates)=>startDeliveryCommand(db,auth,req(key,{contractId:id,truckId:assets.find(a=>a.kind==='truck').assetId,driverId:assets.find(a=>a.kind==='driver').assetId}),x.clock);
  x.claim=(id,key='claim',database=db)=>claimDeliveryRewardCommand(database,auth,req(key,{deliveryId:id}),x.clock);
  x.job=async(key='job',assets=candidates)=>{const g=await x.generate(`${key}_g`,assets),id=g.result.contractIds[0];await x.accept(id,`${key}_a`);const d=await x.start(id,`${key}_s`,assets);x.now=Date.parse(d.result.expectedArrivalAt);return d.result.deliveryId;};
  x.borrow=async(key='borrow')=>{const q=await bankCommand(db,auth,req(`${key}_q`,{requestedMinorUnits:'40'}),'quoteCompanyLoan',x.clock);return bankCommand(db,auth,req(`${key}_t`,{offerId:q.result.offerId}),'takeCompanyLoan',x.clock);};
  x.repay=(id,amount='999',key='repay')=>bankCommand(db,auth,req(key,{loanId:id,requestedMinorUnits:amount}),'repayCompanyLoan',x.clock);
  return x;
 }
 const balance=async x=>(await x.finance.get()).data().balance;
 const snapshot=async x=>{const rows=[];async function visit(ref){const d=await ref.get();rows.push([ref.path,d.data(),d.updateTime]);for(const c of await ref.listCollections())for(const d of (await c.get()).docs)await visit(d.ref);}await visit(x.root);await visit(x.player);return rows.sort((a,b)=>a[0].localeCompare(b[0]));};
 const fault=(mode,onRetry=()=>{})=>{let count=0;return {get attempts(){return count;},db:new Proxy(db,{get(target,key){if(key==='runTransaction')return cb=>target.runTransaction(async tx=>{const r=await cb(tx);count++;if(mode==='rollback')throw Error('controlled rollback');if(count===1){onRetry();throw Object.assign(Error('controlled abort'),{code:10});}return r;});const v=target[key];return typeof v==='function'?v.bind(target):v;}})};};
 try{
 await t.test('M1 Auth/Functions market read is strict, bounded, seed-free, company-safe and write-free',async()=>{
  const x=await setup(),url=`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/getMyMarketState`;
  assert.equal((await post(url,{data:{protocolVersion:1}})).status,401);
  for(const extra of ['companyId','marketVersion','epoch','seed','multiplier','demandIndex'])assert.equal((await post(url,{data:{protocolVersion:1,[extra]:'foreign'}},x.token)).status,400);
  const before=await snapshot(x),r=await post(url,{data:{protocolVersion:1}},x.token);assert.equal(r.status,200);const result=r.body.result.result;
  assert.equal(result.synthetic,true);assert.ok(result.conditions.length<=80);assert.ok(!JSON.stringify(result).includes('synthetic_seed'));assert.deepEqual(await snapshot(x),before);
  x.now=59999;assert.equal((await x.read()).result.epoch,0);x.now=60000;assert.equal((await x.read()).result.epoch,1);assert.equal(await balance(x),100);assert.equal((await x.progression.get()).data().xp,0);
 });
 await t.test('M2 missing/corrupt catalog, selector, hashes and unsupported policy fail closed without offers',async()=>{
  const x=await setup();for(const patch of [null,{approval:'production'},{policyHash:'bad'},{currency:'USD'},{version:'foreign'},{regions:[{...x.market.regions[0],cityId:'foreign'}]}]){
   if(patch)await x.catalog.update(patch);else await x.catalog.delete();const before=await snapshot(x);await assert.rejects(x.read());await assert.rejects(x.generate());assert.deepEqual(await snapshot(x),before);await x.catalog.set(x.market);
  }
  const selector=(await x.selector.get()).data();await x.selector.delete();await assert.rejects(x.read());await x.selector.set({...selector,contractHash:'bad'});await assert.rejects(x.read());await x.selector.set(selector);
  const config=db.collection('v2ContractCatalog').doc(x.version);await config.update({schemaVersion:5});await assert.rejects(x.generate());await config.set(x.config);
 });
 await t.test('M3 neutral, high demand and high supply adjust new offers exactly within caps',async()=>{
  for(const [demand,supply,gross,bps] of [[50,50,100,0],[100,0,115,1500],[0,100,90,-1000]]){
   const x=await setup({demand,supply}),g=await x.generate(),c=(await x.root.collection('contracts').doc(g.result.contractIds[0]).get()).data();
   assert.equal(c.schemaVersion,4);assert.equal(c.terms.reward,gross);assert.equal(c.terms.settlement.grossReward,gross);assert.equal(c.terms.settlement.netReward,gross-4);assert.equal(c.terms.market.snapshot.adjustmentBps,bps);assert.equal(await balance(x),100);
  }
 });
 await t.test('M4 unprofitable whole pool and forged epoch/payload reject without partial records',async()=>{
  const x=await setup({gross:4,demand:0,supply:100});let before=await snapshot(x);await assert.rejects(x.generate());assert.deepEqual(await snapshot(x),before);
  const y=await setup();before=await snapshot(y);await assert.rejects(y.generate('future',candidates,db,Math.floor(y.now/600000)+1));
  await assert.rejects(generateContractOffers(db,y.auth,req('spoof',{multiplier:11500}),{configVersion:y.version,epoch:Math.floor(y.now/600000),assets:candidates},y.clock));assert.deepEqual(await snapshot(y),before);
 });
 await t.test('M5 eight real generation transactions create one immutable pool and replay cannot refresh it',async()=>{
  const x=await setup({jitter:10}),results=await Promise.allSettled(Array.from({length:8},(_,i)=>x.generate(`g${i}`)));const winner=results.findIndex(r=>r.status==='fulfilled');assert.ok(winner>=0);assert.equal(results.filter(r=>r.status==='fulfilled').length,1);
  assert.equal((await x.root.collection('contractPools').get()).size,1);assert.equal((await x.root.collection('contracts').get()).size,3);const before=await snapshot(x);x.now+=60000;
  assert.deepEqual(await x.generate(`g${winner}`),results[winner].value);await assert.rejects(x.generate('different'));assert.deepEqual(await snapshot(x),before);
 });
 await t.test('M6 different companies see the same market; wallet and loan never personalize prices',async()=>{
  const a=await setup({jitter:10}),b=await setup({shared:a});b.now=a.now;await a.borrow();await b.finance.update({balance:500});
  assert.deepEqual((await a.read()).result,(await b.read()).result);const [ga,gb]=await Promise.all([a.generate(),b.generate()]);
  const ta=(await a.root.collection('contracts').doc(ga.result.contractIds[0]).get()).data().terms,tb=(await b.root.collection('contracts').doc(gb.result.contractIds[0]).get()).data().terms;
  assert.deepEqual(ta.market,tb.market);assert.equal(ta.reward,tb.reward);await assert.rejects(b.accept(ga.result.contractIds[0]));
 });
 await t.test('M7 epoch changes before acceptance, start and claim preserve original gross, costs, XP and historical receipts',async()=>{
  const x=await setup({jitter:15}),g=await x.generate(),id=g.result.contractIds[0],ref=x.root.collection('contracts').doc(id),original=(await ref.get()).data().terms;
  x.now+=60000;const accepted=await x.accept(id);assert.equal(accepted.result.reward,String(original.reward));x.now+=60000;const d=await x.start(id);x.now+=60000;
  const paid=await x.claim(d.result.deliveryId);assert.equal(paid.result.reward,String(original.reward));assert.equal(paid.result.settlement.settlementVersion,3);assert.equal(paid.result.bankRepayment.total,'0');assert.equal(paid.result.balance,String(100+original.reward-4));assert.equal(paid.result.xp,10);
  assert.deepEqual((await ref.get()).data().terms,original);assert.deepEqual(await x.accept(id),accepted);assert.deepEqual(await x.claim(d.result.deliveryId),paid);
  assert.equal((await x.root.collection('deliveries').doc(d.result.deliveryId).get()).data().schemaVersion,4);
  assert.equal((await readMyContracts(db,x.auth,{protocolVersion:1},x.clock)).result.items.length,3);assert.equal((await readMyDeliveries(db,x.auth,{protocolVersion:1},x.clock)).result.items.length,1);
 });
 await t.test('M8 pinned snapshot/catalog corruption blocks start, claim and completed reads without repair',async()=>{
  const x=await setup(),g=await x.generate(),id=g.result.contractIds[0],ref=x.root.collection('contracts').doc(id);await x.accept(id);const original=(await ref.get()).data();
  for(const patch of [{'terms.market':null},{'terms.market.snapshot.demandIndex':99},{'terms.market.snapshot.marketHash':'bad'}]){await ref.update(patch);const before=await snapshot(x);await assert.rejects(x.start(id));assert.deepEqual(await snapshot(x),before);await ref.set(original);}
  const d=await x.start(id);x.now=Date.parse(d.result.expectedArrivalAt);await x.catalog.update({seed:'changed'});let before=await snapshot(x);await assert.rejects(x.claim(d.result.deliveryId));assert.deepEqual(await snapshot(x),before);await x.catalog.set(x.market);
  await x.claim(d.result.deliveryId);await x.catalog.delete();before=await snapshot(x);await assert.rejects(readMyDeliveries(db,x.auth,{protocolVersion:1},x.clock));assert.deepEqual(await snapshot(x),before);await x.catalog.set(x.market);
  await x.selector.update({contractVersion:'future_missing'});assert.equal((await readMyDeliveries(db,x.auth,{protocolVersion:1},x.clock)).result.items.length,1);
 });
 await t.test('M9 V4 active loan uses exactly V3 accounting and market price survives payoff',async()=>{
  const x=await setup({demand:100,supply:0}),loan=await x.borrow(),id=await x.job();const marketBefore=(await x.read()).result;const p=await x.claim(id);
  assert.equal(p.result.reward,'115');assert.equal(p.result.settlement.netReward,'111');assert.equal(p.result.bankRepayment.total,'38');assert.equal(p.result.bankRepayment.fee,'3');assert.equal(p.result.bankRepayment.principal,'35');assert.equal(p.result.balance,'213');
  const op=operationId(x.cid,`delivery_${id}`),j=(await x.root.collection('settlementJournals').doc(op).get()).data(),l=(await x.root.collection('ledger').doc(op).get()).data(),claim=(await x.root.collection('claims').doc(`delivery_${id}`).get()).data();
  assert.equal(j.settlementVersion,3);assert.equal(j.schemaVersion,2);assert.equal(l.delta,73);assert.equal(j.balanceAfter-j.previousBalance,l.delta);assert.equal(claim.operationId,op);assert.equal(j.repayment.total,38);
  await x.repay(loan.result.loanId);assert.deepEqual((await x.read()).result,marketBefore);assert.deepEqual(await x.claim(id),p);assert.equal((await readMyDeliveries(db,x.auth,{protocolVersion:1},x.clock)).result.items.length,1);
 });
 await t.test('M10 manual repayment versus dynamic claim is a real conserving transaction race',async()=>{
  for(const amount of ['2','999']){const x=await setup(),loan=await x.borrow(),id=await x.job(),results=await Promise.allSettled([x.repay(loan.result.loanId,amount),x.claim(id)]);assert.equal(results[1].status,'fulfilled');
   const l=(await x.root.collection('loans').doc(loan.result.loanId).get()).data(),paid=43-l.remainingPrincipal-l.remainingFee;assert.ok(paid<=43);assert.equal(await balance(x),236-paid);
  }
 });
 await t.test('M11 two real concurrent dynamic deliveries preserve credits and cap shared loan debt',async()=>{
  const x=await setup(),loan=await x.borrow(),first=await x.job(),assets=[];
  for(const kind of ['truck','driver']){const assetId=`other_${kind}`;assets.push({assetId,kind});await x.root.collection('assets').doc(assetId).set({schemaVersion:1,assetId,kind,companyId:x.cid,catalogId:`test_${kind}`,catalogVersion:x.version,catalogHash:digest(x.logistics),status:'operational',assignmentId:null,acquiredAt:Timestamp.now()});}
  x.now+=600000;const second=await x.job('second',assets);await Promise.all([x.claim(first,'first_claim'),x.claim(second,'second_claim')]);assert.equal(await balance(x),289);
  assert.equal((await x.root.collection('loans').doc(loan.result.loanId).get()).data().status,'paid');assert.equal((await x.root.collection('settlementJournals').get()).size,2);
 });
 await t.test('M12 eight dynamic delivery claims create one financial effect and one receipt',async()=>{
  const x=await setup(),loan=await x.borrow(),id=await x.job(),r=await Promise.allSettled(Array.from({length:8},(_,i)=>x.claim(id,`c${i}`)));
  assert.equal(r.filter(v=>v.status==='fulfilled').length,1);assert.equal(await balance(x),203);assert.equal((await x.root.collection('loans').doc(loan.result.loanId).collection('repayments').get()).size,1);
  const winner=r.findIndex(v=>v.status==='fulfilled');assert.deepEqual(await x.claim(id,`c${winner}`),r[winner].value);
 });
 await t.test('M13 final wallet maximum and XP overflow reject atomically, combined net repayment fits',async()=>{
  const x=await setup(),loan=await x.borrow(),id=await x.job();await x.finance.update({balance:9950});let before=await snapshot(x);await assert.rejects(x.claim(id));assert.deepEqual(await snapshot(x),before);
  await x.finance.update({balance:9930});const p=await x.claim(id);assert.equal(p.result.balance,'9993');assert.equal(p.result.bankRepayment.total,'33');assert.equal(p.result.bankRepayment.loanId,loan.result.loanId);
  const y=await setup(),job=await y.job();await y.progression.update({xp:1000,level:3});before=await snapshot(y);await assert.rejects(y.claim(job));assert.deepEqual(await snapshot(y),before);
 });
 await t.test('M14 queued writes roll back; actual SDK retry retains deterministic quote and boundary crossing fails closed',async()=>{
  const x=await setup({jitter:10});let before=await snapshot(x);await assert.rejects(x.generate('rollback',candidates,fault('rollback').db));assert.deepEqual(await snapshot(x),before);
  const retry=fault('retry'),expected=marketQuote(x.market,marketEpoch(x.now,x.market.periodMs),'synthetic_destination','synthetic_cargo',100),g=await x.generate('retry',candidates,retry.db);assert.ok(retry.attempts>=2);assert.deepEqual((await x.root.collection('contracts').doc(g.result.contractIds[0]).get()).data().terms.market,expected);
  await x.accept(g.result.contractIds[0]);const d=await x.start(g.result.contractIds[0]);x.now=Date.parse(d.result.expectedArrivalAt);before=await snapshot(x);await assert.rejects(x.claim(d.result.deliveryId,'rollback_claim',fault('rollback').db));assert.deepEqual(await snapshot(x),before);
  const y=await setup({jitter:10}),cross=fault('retry',()=>{y.now+=60000;});before=await snapshot(y);await assert.rejects(y.generate('cross',candidates,cross.db));assert.deepEqual(await snapshot(y),before);
 });
 await t.test('M15 post-commit lost response recovers original market receipt without regeneration',async()=>{
  const x=await setup({jitter:10});let lose=true;const proxy=new Proxy(db,{get(target,key){if(key==='collection')return path=>{const c=target.collection(path);if(path!=='v2Players')return c;const doc=c.doc.bind(c);c.doc=uid=>{const p=doc(uid),sub=p.collection.bind(p);p.collection=name=>{const s=sub(name),d=s.doc.bind(s);s.doc=k=>{const ref=d(k),get=ref.get.bind(ref);ref.get=async(...args)=>{if(k==='lost'&&lose){lose=false;throw Object.assign(Error('controlled response loss'),{code:14});}return get(...args);};return ref;};return s;};return p;};return c;};const v=target[key];return typeof v==='function'?v.bind(target):v;}});
  await assert.rejects(x.generate('lost',candidates,proxy),e=>e.details.code==='RETRY_LATER');const before=await snapshot(x);x.now+=60000;const recovered=await x.generate('lost'),receipt=(await x.player.collection('commands').doc('lost').get()).data();assert.deepEqual(recovered.result,receipt.result);assert.deepEqual(await snapshot(x),before);assert.equal(recovered.serverTime,receipt.committedAt.toDate().toISOString());
 });
 await t.test('M16 pending onboarding and migration reject market access and generation without repair',async()=>{
  const pending=await setup({pending:true});await assert.rejects(pending.read());await assert.rejects(pending.generate());
  const x=await setup(),m=db.doc(`v2MigrationRecords/${x.uid}`);configs.push(m);await m.set({synthetic:true});const before=await snapshot(x);await assert.rejects(x.read());await assert.rejects(x.generate());assert.deepEqual(await snapshot(x),before);
 });
 }finally{
  for(const root of roots)await db.recursiveDelete(root);for(const ref of configs)await ref.delete();
  for(const token of tokens)assert.equal((await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:delete?key=synthetic-key',{idToken:token})).status,200);
  await db.terminate();if(previous===undefined)delete process.env.FUNCTIONS_EMULATOR;else process.env.FUNCTIONS_EMULATOR=previous;
 }
});

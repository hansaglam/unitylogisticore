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
const {generateContractOffers,acceptContractCommand}=await import('../functions/lib/contracts.js');
const {startDeliveryCommand,claimDeliveryRewardCommand,readMyDeliveries}=await import('../functions/lib/deliveries.js');
const {initializeBank,bankCommand,readMyBankState}=await import('../functions/lib/bank.js');
const {digest}=await import('../functions/lib/logisticsModel.js');
const {bankFixtures}=await import('./bank.fixtures.mjs');
const {candidates}=await import('./contracts.fixtures.mjs');
const req=(key,payload={})=>({protocolVersion:1,idempotencyKey:key,payload});
// Allow bounded local Functions cold-start time; this is not a production latency SLA.
async function post(url,data,token){const r=await fetch(url,{method:'POST',redirect:'error',headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:JSON.stringify(data),signal:AbortSignal.timeout(60000)});return {status:r.status,body:await r.json()};}
const endpoint=n=>`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/${n}`;
test('BANK-01: real emulator lifecycle, financial conservation, V3 and concurrency',{timeout:360000},async t=>{
 const previous=process.env.FUNCTIONS_EMULATOR;process.env.FUNCTIONS_EMULATOR='true';const db=createLocalFirestore(),roots=[],configs=[],tokens=[];
 const fault=mode=>{let count=0;return {get attempts(){return count;},db:new Proxy(db,{get(target,key){if(key==='runTransaction')return cb=>target.runTransaction(async tx=>{const r=await cb(tx);count++;if(mode==='rollback')throw Error('controlled rollback');if(count===1)throw Object.assign(Error('controlled SDK retry'),{code:10});return r;});const v=target[key];return typeof v==='function'?v.bind(target):v;}})};};
 async function setup(options={}){
  const signed=await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=synthetic-key',{returnSecureToken:true});assert.equal(signed.status,200);
  const uid=signed.body.localId,token=signed.body.idToken;tokens.push(token);const auth={uid},cid=companyIdForUid(uid),root=db.doc(`v2Companies/${cid}`),version=`bank_${randomUUID()}`;
  roots.push(root,db.doc(`v2Players/${uid}`));const f=bankFixtures(version,options);
  for(const [collection,value] of Object.entries({v2WorldConfig:f.economy,v2LogisticsCatalog:f.logistics,v2OperatingCostCatalog:f.costCatalog,v2ContractCatalog:f.config,v2DeliveryRules:f.rules,v2BankCatalog:f.bank})){
   const ref=db.collection(collection).doc(version);configs.push(ref);await ref.set(value);
  }
  await initializeIdentity(db,auth,{protocolVersion:1});await createCompanyCommand(db,auth,req('create',{name:'Synthetic Bank Company'}));
  await runEconomyCommand(db,auth,req('economy'),{kind:'initialize',id:version});if(!options.pending)await provisionStarterFleet(db,auth,req('starter'),version);
  const x={...f,uid,token,auth,cid,root,version,now:Math.floor(Date.now()/600000)*600000+1000,finance:root.collection('state').doc('finance'),progression:root.collection('state').doc('progression'),anchor:root.collection('bank').doc('current'),catalog:db.collection('v2BankCatalog').doc(version)};
  x.clock=()=>x.now;x.command=(kind,key,payload,database=db)=>bankCommand(database,auth,req(key,payload),kind,x.clock);
  x.quote=(key='quote',amount='10',database=db)=>x.command('quoteCompanyLoan',key,{requestedMinorUnits:amount},database);
  x.take=(id,key='take',database=db)=>x.command('takeCompanyLoan',key,{offerId:id},database);
  x.repay=(id,amount='999',key='repay',database=db)=>x.command('repayCompanyLoan',key,{loanId:id,requestedMinorUnits:amount},database);
  x.read=()=>readMyBankState(db,auth,{protocolVersion:1},x.clock);
  x.borrow=async(key='cycle',amount='10')=>{const q=await x.quote(`${key}_q`,amount);return x.take(q.result.offerId,`${key}_t`);};
  x.job=async(key='job',assets=candidates)=>{const epoch=Math.floor(x.now/600000);const g=await generateContractOffers(db,auth,req(`${key}_g`),{configVersion:version,epoch,assets},x.clock);
   const id=g.result.contractIds[0];await acceptContractCommand(db,auth,req(`${key}_a`,{contractId:id}),x.clock);
   const s=await startDeliveryCommand(db,auth,req(`${key}_s`,{contractId:id,truckId:assets.find(a=>a.kind==='truck').assetId,driverId:assets.find(a=>a.kind==='driver').assetId}),x.clock);x.now=Date.parse(s.result.expectedArrivalAt);return s.result.deliveryId;};
  x.claim=(id,key='claim',database=db)=>claimDeliveryRewardCommand(database,auth,req(key,{deliveryId:id}),x.clock);
  x.loan=id=>root.collection('loans').doc(id);
  if(!options.noBank&&!options.pending)await initializeBank(db,auth,req('bank'),version);return x;
 }
 const balance=async x=>(await x.finance.get()).data().balance;
 const snap=async x=>{const docs=[];const visit=async ref=>{const d=await ref.get();docs.push([ref.path,d.data(),d.updateTime]);for(const col of await ref.listCollections())for(const child of (await col.get()).docs)await visit(child.ref);};await visit(x.root);return docs.sort((a,b)=>a[0].localeCompare(b[0]));};
 try{
 await t.test('trusted bootstrap requires full readiness, no free funds, no duplicate initialization, missing anchor read fails',async()=>{
  const x=await setup({noBank:true}),before=await balance(x);await assert.rejects(x.read());await assert.rejects(x.quote());
  await initializeBank(db,x.auth,req('bank'),x.version);assert.equal(await balance(x),before);assert.equal((await x.read()).result.level,1);
  await assert.rejects(initializeBank(db,x.auth,req('twice'),x.version));assert.equal((await x.root.collection('loans').get()).size,0);
  const pending=await setup({pending:true});await assert.rejects(initializeBank(db,pending.auth,req('bank'),pending.version));await assert.rejects(pending.quote());
  await x.anchor.update({sequence:-1});await assert.rejects(x.read());
 });
 await t.test('catalog hash, currency, scale, policy, tier corruption and migration deny without wallet mutation',async()=>{
  const x=await setup(),before=await balance(x);
  for(const patch of [{currency:'LGC'},{scale:6},{economyHash:'0'.repeat(64)},{approval:'production'},{bankPolicyHash:'0'.repeat(64)},{tiers:[{from:2,to:3,maximum:40}]}]){
   await x.catalog.update(patch);await assert.rejects(x.quote());await assert.rejects(x.read());await x.catalog.set(x.bank);
  }
  const migration=db.doc(`v2MigrationRecords/${x.uid}`);configs.push(migration);await migration.set({synthetic:true});await assert.rejects(x.quote(),e=>e.details.code==='MIGRATION_REQUIRED');await migration.delete();assert.equal(await balance(x),before);
 });
 await t.test('min/max/custom and strict wire/grid/wallet bounds; spoofed authority denied',async()=>{
  const x=await setup();for(const amount of ['10','40','20'])assert.equal((await x.quote(`q${amount}`,amount)).result.principal,amount);
  for(const amount of ['0','-1','1e1','10.0','01','9007199254740992','5','50','15',10])await assert.rejects(x.quote(`bad_${String(amount).replace(/[^a-z0-9]/gi,'_')}`,amount));
  await assert.rejects(x.command('quoteCompanyLoan','spoof',{requestedMinorUnits:'10',companyId:x.cid}));await x.finance.update({balance:9995});await assert.rejects(x.quote('full'));
 });
 await t.test('quote reuse, replacement, cancel, expiry boundary, replay never extends time and budget is enforced',async()=>{
  const x=await setup(),q=await x.quote(),r=await x.quote('reuse');assert.equal(q.result.offerId,r.result.offerId);const replace=await x.quote('replace','20');
  await assert.rejects(x.take(q.result.offerId));await x.command('cancelCompanyLoanQuote','cancel',{offerId:replace.result.offerId});await assert.rejects(x.take(replace.result.offerId));
  const exp=await x.quote('expiry');x.now=Date.parse(exp.result.expiresAt);await assert.rejects(x.take(exp.result.offerId,'expired'),e=>e.details.code==='OFFER_EXPIRED');assert.deepEqual(await x.quote('expiry'),exp);
  x.now+=60000;const results=await Promise.allSettled(Array.from({length:21},(_,i)=>x.quote(`budget_${i}`)));
  t.diagnostic(`budget first attempts: ${JSON.stringify(results.map(r=>r.status==='fulfilled'?'ok':r.reason.details?.code))}`);
  // SDK contention retries are bounded. Honor the public RETRY_LATER contract with
  // the SAME intent/key; never turn an unexpected failure into an accepted result.
  for(let i=0;i<results.length;i++)if(results[i].status==='rejected'){
   assert.ok(['BANK_RATE_LIMITED','RETRY_LATER'].includes(results[i].reason.details?.code));
   if(results[i].reason.details.code==='RETRY_LATER')results[i]=(await Promise.allSettled([x.quote(`budget_${i}`)]))[0];
  }
  assert.equal(results.filter(r=>r.status==='fulfilled').length,20);assert.equal(results.filter(r=>r.status==='rejected'&&r.reason.details.code==='BANK_RATE_LIMITED').length,1);
  assert.equal((await x.anchor.get()).data().requests,20);
 });
 await t.test('eight genuine concurrent quotes reuse one right; eight originations issue once without XP or revenue',async()=>{
  const x=await setup();const quotes=await Promise.all(Array.from({length:8},(_,i)=>x.quote(`q${i}`)));assert.equal(new Set(quotes.map(q=>q.result.offerId)).size,1);
  const offers=await x.root.collection('loanOffers').get();assert.equal(offers.size,1);const id=quotes[0].result.offerId;
  const attempts=await Promise.allSettled(Array.from({length:8},(_,i)=>x.take(id,`take${i}`)));assert.equal(attempts.filter(r=>r.status==='fulfilled').length,1);
  const winner=attempts.findIndex(r=>r.status==='fulfilled'),paid=attempts[winner].value;assert.deepEqual(await x.take(id,`take${winner}`),paid);
  assert.equal(await balance(x),110);assert.equal((await x.progression.get()).data().xp,0);assert.equal(paid.result.liability,'11');assert.equal((await x.root.collection('loans').get()).size,1);
  await assert.rejects(x.take(id,'different'));await assert.rejects(x.quote('active'));
 });
 await t.test('partial fee-first payment, capped payoff, distinct reborrow IDs, historical replay and cash conservation across cycles',async()=>{
  const x=await setup(),first=await x.borrow('one','40'),loan=first.result.loanId;let p=await x.repay(loan,'2','partial');assert.equal(p.result.fee,'2');assert.equal(p.result.principal,'0');
  p=await x.repay(loan,'999','payoff');assert.equal(p.result.actual,'41');assert.equal(p.result.status,'paid');assert.equal(await balance(x),97);
  const history=(await x.loan(loan).get()).data();await assert.rejects(x.repay(loan,'1','paid'));const second=await x.borrow('two');assert.notEqual(second.result.loanId,loan);
  assert.deepEqual(await x.take((await x.loan(loan).get()).data().offerId,'one_t'),first);assert.deepEqual(await x.repay(loan,'999','payoff'),p);assert.deepEqual((await x.loan(loan).get()).data(),history);
  await x.repay(second.result.loanId,'999','second_pay');const third=await x.borrow('three');await x.repay(third.result.loanId,'999','third_pay');assert.equal(await balance(x),95);
  await assert.rejects(x.take(history.offerId,'oldquote'));await assert.rejects(x.repay(loan,'1','payoff'),e=>e.details.code==='IDEMPOTENCY_CONFLICT');
 });
 await t.test('reserve protection, zero capacity and wallet/debt/anchor corruption fail closed',async()=>{
  const x=await setup(),b=await x.borrow();await x.finance.update({balance:5});await assert.rejects(x.repay(b.result.loanId),e=>e.details.code==='BANK_NO_CAPACITY');
  await x.finance.update({balance:7});assert.equal((await x.repay(b.result.loanId,'9','limited')).result.actual,'2');assert.equal(await balance(x),5);
  const ref=x.loan(b.result.loanId),original=(await ref.get()).data();await ref.update({remainingPrincipal:1});await assert.rejects(x.read());await ref.set(original);
  await x.finance.update({balance:-1});await assert.rejects(x.read());await x.finance.update({balance:5});await x.anchor.update({activeLoanId:null});await assert.rejects(x.read());
 });
 await t.test('level snapshot invalidates quote; future limits grow while existing loan terms remain pinned',async()=>{
  const x=await setup(),q=await x.quote();await x.progression.update({xp:10,level:2});await assert.rejects(x.take(q.result.offerId));assert.equal((await x.read()).result.maximumPrincipal,'60');
  const b=await x.borrow('higher','60'),saved=(await x.loan(b.result.loanId).get()).data().terms;await x.progression.update({xp:50,level:3});assert.deepEqual((await x.loan(b.result.loanId).get()).data().terms,saved);
  await x.repay(b.result.loanId);assert.equal((await x.quote('next','80')).result.principal,'80');
 });
 await t.test('foreign company offer and loan IDs cannot alter either wallet',async()=>{
  const a=await setup(),b=await setup(),q=await a.quote();await assert.rejects(b.take(q.result.offerId));const issued=await a.take(q.result.offerId);await assert.rejects(b.repay(issued.result.loanId));assert.equal(await balance(b),100);
 });
 await t.test('V1 and V2 never repay active loans; completed reads and historical receipts remain compatible',async()=>{
  for(const legacy of [1,2]){const x=await setup({legacy}),loan=await x.borrow(),id=await x.job(),p=await x.claim(id);assert.equal(p.result.balance,String(legacy===1?134:130));
   assert.equal((await x.read()).result.loan.remainingDebt,'11');assert.equal(Object.hasOwn(p.result,'bankRepayment'),false);assert.equal((await readMyDeliveries(db,x.auth,{protocolVersion:1},x.clock)).result.items.length,1);assert.deepEqual(await x.claim(id),p);assert.equal((await x.loan(loan.result.loanId).get()).data().paymentSequence,0);}
 });
 await t.test('V3 no active loan preserves net; loan issued after acceptance is selected at claim snapshot',async()=>{
  const x=await setup(),id=await x.job(),p=await x.claim(id);assert.equal(p.result.bankRepayment.total,'0');assert.equal(p.result.balance,'120');
  x.now+=600000;const next=await x.job('later'),loan=await x.borrow(),paid=await x.claim(next,'later_claim');assert.equal(paid.result.bankRepayment.loanId,loan.result.loanId);
  assert.equal(paid.result.bankRepayment.total,'7');assert.equal(paid.result.bankRepayment.fee,'1');assert.equal(paid.result.bankRepayment.principal,'6');assert.equal(paid.result.balance,'143');assert.equal(paid.result.xp,20);
  assert.equal((await readMyDeliveries(db,x.auth,{protocolVersion:1},x.clock)).result.items.length,2);
 });
 await t.test('eight genuine V3 claims create one payment; journal, ledger, debt and XP agree',async()=>{
  const x=await setup(),loan=await x.borrow(),id=await x.job();const results=await Promise.allSettled(Array.from({length:8},(_,i)=>x.claim(id,`claim${i}`)));assert.equal(results.filter(r=>r.status==='fulfilled').length,1);
  assert.equal(await balance(x),123);assert.equal((await x.read()).result.loan.remainingDebt,'4');assert.equal((await x.loan(loan.result.loanId).collection('repayments').get()).size,1);
  const op=operationId(x.cid,`delivery_${id}`),j=(await x.root.collection('settlementJournals').doc(op).get()).data(),l=(await x.root.collection('ledger').doc(op).get()).data();
  assert.equal(l.delta,13);assert.equal(l.delta,j.balanceAfter-j.previousBalance);assert.equal(j.repayment.total,7);assert.equal(j.quote.netReward,20);assert.equal((await x.progression.get()).data().xp,10);
 });
 await t.test('manual versus auto real race, including final payoff, conserves cash and never overpays',async()=>{
  for(const amount of ['2','999']){const x=await setup(),loan=await x.borrow(),id=await x.job();const results=await Promise.allSettled([x.repay(loan.result.loanId,amount),x.claim(id)]);assert.equal(results[1].status,'fulfilled');
   const l=(await x.loan(loan.result.loanId).get()).data(),total=11-l.remainingPrincipal-l.remainingFee;assert.ok(total<=11);assert.equal(await balance(x),130-total);await x.read();}
 });
 await t.test('two distinct concurrent deliveries cap final debt and release both assignments',async()=>{
  const x=await setup(),loan=await x.borrow(),first=await x.job(),assets=[];for(const kind of ['truck','driver']){const assetId=`other_${kind}`;assets.push({assetId,kind});await x.root.collection('assets').doc(assetId).set({schemaVersion:1,assetId,kind,companyId:x.cid,catalogId:`test_${kind}`,catalogVersion:x.version,catalogHash:digest(x.logistics),status:'operational',assignmentId:null,acquiredAt:Timestamp.now()});}
  x.now+=600000;const second=await x.job('second',assets);await Promise.all([x.claim(first,'first_claim'),x.claim(second,'second_claim')]);assert.equal(await balance(x),139);
  assert.equal((await x.loan(loan.result.loanId).get()).data().status,'paid');assert.equal((await x.anchor.get()).data().activeLoanId,null);await x.read();
 });
 await t.test('payoff vs quote race and immediate reborrow vs delivery origination race never touch the wrong lifecycle',async()=>{
  const x=await setup(),a=await x.borrow(),id=await x.job();const race=await Promise.allSettled([x.repay(a.result.loanId),x.quote('next')]);assert.equal(race[0].status,'fulfilled');
  const quote=race[1].status==='fulfilled'?race[1].value:await x.quote('retry_next');const results=await Promise.allSettled([x.take(quote.result.offerId,'newloan'),x.claim(id)]);
  // A claim can invalidate the quote's XP snapshot; either serial outcome is valid.
  assert.equal(results[1].status,'fulfilled');const l=(await x.loan(a.result.loanId).get()).data();assert.equal(l.status,'paid');assert.equal(l.remainingPrincipal,0);
  await x.read();if(results[0].status==='fulfilled')assert.notEqual(results[0].value.result.loanId,a.result.loanId);
 });
 await t.test('final committed balance fits even when pre-repayment intermediate cash exceeds maximum',async()=>{
  const x=await setup(),loan=await x.borrow('loan','40'),id=await x.job();await x.finance.update({balance:9985});const p=await x.claim(id);assert.equal(p.result.balance,'9998');assert.equal(p.result.bankRepayment.total,'7');assert.equal((await x.loan(loan.result.loanId).get()).data().remainingPrincipal,36);
 });
 await t.test('V3 missing/corrupt journal and config drift fail reads/claims without repair',async()=>{
  const x=await setup(),id=await x.job();const config=db.collection('v2OperatingCostCatalog').doc(x.version);await config.update({policy:'bad'});await assert.rejects(x.claim(id));await config.set(x.costCatalog);
  await x.claim(id);const journal=x.root.collection('settlementJournals').doc(operationId(x.cid,`delivery_${id}`)),saved=(await journal.get()).data();
  for(const patch of [null,{finalWalletDelta:999},{'repayment.total':1},{loanId:'foreign'}]){if(patch)await journal.update(patch);else await journal.delete();await assert.rejects(readMyDeliveries(db,x.auth,{protocolVersion:1},x.clock));await journal.set(saved);}
 });
 await t.test('queued-write rollback on origination/manual/V3 leaves all company documents unchanged; actual SDK aborted retry succeeds once',async()=>{
  const x=await setup(),q=await x.quote();let before=await snap(x);await assert.rejects(x.take(q.result.offerId,'rollback',fault('rollback').db));assert.deepEqual(await snap(x),before);
  const retry=fault('retry'),loan=await x.take(q.result.offerId,'retry',retry.db);assert.ok(retry.attempts>=2);assert.equal(await balance(x),110);
  before=await snap(x);await assert.rejects(x.repay(loan.result.loanId,'2','rollback_payment',fault('rollback').db));assert.deepEqual(await snap(x),before);
  const id=await x.job();before=await snap(x);await assert.rejects(x.claim(id,'rollback_claim',fault('rollback').db));assert.deepEqual(await snap(x),before);
 });
 await t.test('post-commit lost response recovers original origination receipt without a second payout',async()=>{
  const x=await setup(),q=await x.quote();let lose=true;const lossy=new Proxy(db,{get(target,key){if(key==='collection')return path=>{const c=target.collection(path);if(path!=='v2Players')return c;const doc=c.doc.bind(c);c.doc=uid=>{const p=doc(uid),sub=p.collection.bind(p);p.collection=name=>{const s=sub(name),d=s.doc.bind(s);s.doc=k=>{const ref=d(k),get=ref.get.bind(ref);ref.get=async(...args)=>{if(k==='lost'&&lose){lose=false;throw Object.assign(Error('controlled response loss'),{code:14});}return get(...args);};return ref;};return s;};return p;};return c;};const v=target[key];return typeof v==='function'?v.bind(target):v;}});
  await assert.rejects(x.take(q.result.offerId,'lost',lossy),e=>e.details.code==='RETRY_LATER');const recovered=await x.take(q.result.offerId,'lost'),receipt=(await db.doc(`v2Players/${x.uid}/commands/lost`).get()).data();
  assert.deepEqual(recovered.result,receipt.result);assert.equal(recovered.serverTime,receipt.committedAt.toDate().toISOString());assert.equal(await balance(x),110);
 });
 await t.test('genuine Auth/Functions endpoints: unauthenticated denied, reads write nothing, quote/take/repay/cancel exercise public wrappers',async()=>{
  const x=await setup();for(const name of ['getMyBankState','quoteCompanyLoan','takeCompanyLoan','repayCompanyLoan','cancelCompanyLoanQuote'])assert.equal((await post(endpoint(name),{data:{protocolVersion:1}})).status,401);
  const before=await snap(x);const read=await post(endpoint('getMyBankState'),{data:{protocolVersion:1}},x.token);assert.equal(read.status,200);assert.deepEqual(await snap(x),before);
  const q=await post(endpoint('quoteCompanyLoan'),{data:req('api_q',{requestedMinorUnits:'10'})},x.token);assert.equal(q.status,200);const offerId=q.body.result.result.offerId;
  const take=await post(endpoint('takeCompanyLoan'),{data:req('api_take',{offerId})},x.token);assert.equal(take.status,200);
  const pay=await post(endpoint('repayCompanyLoan'),{data:req('api_pay',{loanId:take.body.result.result.loanId,requestedMinorUnits:'999'})},x.token);assert.equal(pay.status,200);assert.equal(pay.body.result.result.status,'paid');
  const q2=await post(endpoint('quoteCompanyLoan'),{data:req('api_q2',{requestedMinorUnits:'20'})},x.token);assert.equal(q2.status,200);assert.equal((await post(endpoint('cancelCompanyLoanQuote'),{data:req('api_cancel',{offerId:q2.body.result.result.offerId})},x.token)).status,200);
  assert.equal((await post(endpoint('getMyBankState'),{data:{protocolVersion:1,companyId:x.cid}},x.token)).status,400);
 });
 await t.test('FINAL A: real concurrent quote replacement versus consumption has one serial financial outcome',async()=>{
  for(let round=0;round<3;round++){
   const x=await setup(),a=await x.quote('a'),race=await Promise.allSettled([x.take(a.result.offerId,'consume_a'),x.quote('replace_b','20')]);
   assert.equal(race.filter(r=>r.status==='fulfilled').length,1);
   const consumed=race[0].status==='fulfilled',anchor=(await x.anchor.get()).data(),offers=(await x.root.collection('loanOffers').get()).docs.map(d=>d.data());
   assert.equal(offers.find(q=>q.offerId===a.result.offerId).status,consumed?'consumed':'replaced');
   assert.equal(offers.filter(q=>q.status==='available').length,consumed?0:1);
   assert.equal(anchor.currentQuoteId,consumed?null:race[1].value.result.offerId);
   assert.equal(anchor.activeLoanId,consumed?race[0].value.result.loanId:null);
   assert.equal((await x.root.collection('loans').get()).size,consumed?1:0);assert.equal(await balance(x),consumed?110:100);
   for(const collection of ['claims','ledger','bankJournals']){
    const records=(await x.root.collection(collection).get()).docs.filter(d=>d.data().source?.startsWith('bank_origination_'));
    assert.equal(records.length,consumed?1:0);
    if(consumed)assert.equal(records[0].data().source,`bank_origination_${anchor.activeLoanId}`);
   }
   for(const [i,key] of ['consume_a','replace_b'].entries()){
    const receipt=await db.doc(`v2Players/${x.uid}/commands/${key}`).get();assert.equal(receipt.exists,race[i].status==='fulfilled');
    if(receipt.exists)assert.deepEqual(receipt.data().result,race[i].value.result);
   }
   assert.equal(race[consumed?1:0].reason.details.code,consumed?'BANK_ACTIVE_LOAN':'BANK_QUOTE_STALE');
   await x.read();t.diagnostic(`FINAL A round ${round+1}: ${consumed?'consumption':'replacement'} won`);
  }
 });
 await t.test('FINAL B: real cancellation versus consumption cannot orphan a quote or reverse a payout',async()=>{
  for(let round=0;round<3;round++){
   const x=await setup(),q=await x.quote(),race=await Promise.allSettled([x.command('cancelCompanyLoanQuote','cancel_a',{offerId:q.result.offerId}),x.take(q.result.offerId,'consume_a')]);
   assert.equal(race.filter(r=>r.status==='fulfilled').length,1);const consumed=race[1].status==='fulfilled',a=(await x.anchor.get()).data();
   assert.equal(a.currentQuoteId,null);assert.equal(a.activeLoanId,consumed?race[1].value.result.loanId:null);
   assert.equal((await x.root.collection('loanOffers').doc(q.result.offerId).get()).data().status,consumed?'consumed':'cancelled');
   assert.equal((await x.root.collection('loans').get()).size,consumed?1:0);assert.equal(await balance(x),consumed?110:100);
   for(const col of ['ledger','claims','bankJournals'])assert.equal((await x.root.collection(col).get()).docs.filter(d=>d.data().source?.startsWith('bank_origination_')).length,consumed?1:0);
   for(const [i,key] of ['cancel_a','consume_a'].entries()){
    const r=await db.doc(`v2Players/${x.uid}/commands/${key}`).get();assert.equal(r.exists,race[i].status==='fulfilled');if(r.exists)assert.deepEqual(r.data().result,race[i].value.result);
   }
   assert.equal(race[consumed?0:1].reason.details.code,'BANK_QUOTE_STALE');await x.read();t.diagnostic(`FINAL B round ${round+1}: ${consumed?'consumption':'cancellation'} won`);
  }
 });
 await t.test('FINAL C: two real concurrent replacements serialize generations and preserve each committed receipt',async()=>{
  const x=await setup(),original=await x.quote('original'),results=await Promise.all([x.quote('replace_20','20'),x.quote('replace_30','30')]);
  const a=(await x.anchor.get()).data(),offers=(await x.root.collection('loanOffers').get()).docs.map(d=>d.data()).sort((a,b)=>a.generation-b.generation);
  assert.deepEqual(offers.map(o=>o.generation),[1,2,3]);assert.deepEqual(offers.map(o=>o.status),['replaced','replaced','available']);
  assert.equal(offers[0].offerId,original.result.offerId);assert.equal(a.currentQuoteId,offers[2].offerId);assert.equal(a.generation,3);assert.equal(a.requests,3);assert.equal(a.writes,3);
  assert.equal(await balance(x),100);assert.equal((await x.root.collection('loans').get()).size,0);
  const before=await snap(x);
  for(const [i,amount] of ['20','30'].entries()){
   const r=(await db.doc(`v2Players/${x.uid}/commands/replace_${amount}`).get()).data();assert.deepEqual(r.result,results[i].result);
   assert.equal(r.result.status,'available');assert.deepEqual(await x.quote(`replace_${amount}`,amount),results[i]);
  }
  assert.deepEqual(await snap(x),before);await x.read();
 });
 await t.test('FINAL D: final payoff versus quote permits a usable offer only after atomic closure',async()=>{
  const x=await setup(),b=await x.borrow(),race=await Promise.allSettled([x.repay(b.result.loanId,'11','exact_payoff'),x.quote('after_payoff','20')]);
  assert.equal(race[0].status,'fulfilled');assert.equal(race[0].value.result.actual,'11');const a=(await x.anchor.get()).data(),loan=(await x.loan(b.result.loanId).get()).data();
  assert.equal(loan.status,'paid');assert.equal(loan.remainingPrincipal+loan.remainingFee,0);assert.equal(a.activeLoanId,null);assert.equal(a.lastClosedLoanId,loan.loanId);assert.equal(await balance(x),99);
  const receipt=await db.doc(`v2Players/${x.uid}/commands/after_payoff`).get();
  if(race[1].status==='fulfilled'){
   assert.equal(a.currentQuoteId,race[1].value.result.offerId);assert.ok(receipt.data().committedAt.toMillis()>=loan.closedAt.toMillis());
   assert.equal((await x.root.collection('loanOffers').doc(a.currentQuoteId).get()).data().sequence,1);
  }else{assert.equal(race[1].reason.details.code,'BANK_ACTIVE_LOAN');assert.equal(a.currentQuoteId,null);assert.equal(receipt.exists,false);}
  t.diagnostic(`FINAL D: quote ${race[1].status}`);await x.read();
 });
 await t.test('FINAL E: historical quote replay after replacement and disbursement writes nothing or resets budgets',async()=>{
  const x=await setup(),a=await x.quote('historical'),b=await x.quote('replacement','20');
  const receiptRef=db.doc(`v2Players/${x.uid}/commands/historical`),receipt=await receiptRef.get();
  for(const phase of ['replaced','consumed']){
   if(phase==='consumed')await x.take(b.result.offerId,'consume_b');x.now+=phase==='consumed'?120000:1000;
   const before=await snap(x);assert.deepEqual(await x.quote('historical'),a);assert.deepEqual(await snap(x),before);
   const after=await receiptRef.get();assert.ok(after.updateTime.isEqual(receipt.updateTime));assert.deepEqual(after.data(),receipt.data());
   assert.equal((await x.root.collection('loanOffers').doc(a.result.offerId).get()).data().status,'replaced');
  }
  assert.equal(await balance(x),120);assert.equal((await x.root.collection('loans').get()).size,1);
 });
 await t.test('FINAL evidence corruption: payment source and manual journal identity, terms and balances fail closed',async()=>{
  const x=await setup(),b=await x.borrow();await x.repay(b.result.loanId,'2','partial');const loan=(await x.loan(b.result.loanId).get()).data();
  const history=x.loan(loan.loanId).collection('repayments').doc(loan.lastPaymentSource),saved=(await history.get()).data();
  await history.update({source:'wrong_source'});await assert.rejects(x.read(),e=>e.details.code==='FAILED_PRECONDITION');await history.set(saved);
  const journal=x.root.collection('bankJournals').doc(operationId(x.cid,loan.lastPaymentSource)),original=(await journal.get()).data();
  for(const patch of [{schemaVersion:2},{kind:'origination'},{companyId:'foreign'},{operationId:'wrong'},{termsHash:'0'.repeat(64)},{previousBalance:0},{balanceAfter:0}]){
   await journal.update(patch);const before=await snap(x);await assert.rejects(x.read(),e=>e.details.code==='FAILED_PRECONDITION');await assert.rejects(x.repay(loan.loanId,'1','corrupt'),e=>e.details.code==='FAILED_PRECONDITION');assert.deepEqual(await snap(x),before);await journal.set(original);
  }
  await x.read();
 });
 await t.test('FINAL V3: closed-before-claim and a new cycle select current debt while historical settlements stay immutable',async()=>{
  const x=await setup(),a=await x.borrow('first'),id=await x.job();await x.repay(a.result.loanId,'999','close_a');
  const closed=(await x.loan(a.result.loanId).get()).data(),paid=await x.claim(id,'no_loan_claim');assert.equal(paid.result.bankRepayment.loanId,null);assert.equal(paid.result.bankRepayment.total,'0');
  const journal=x.root.collection('settlementJournals').doc(operationId(x.cid,`delivery_${id}`)),historical=(await journal.get()).data();
  x.now+=600000;const next=await x.job('new_job'),b=await x.borrow('second');const result=await x.claim(next,'new_claim');
  assert.equal(result.result.bankRepayment.loanId,b.result.loanId);assert.notEqual(b.result.loanId,a.result.loanId);assert.equal(result.result.bankRepayment.total,'7');
  assert.deepEqual((await x.loan(a.result.loanId).get()).data(),closed);assert.deepEqual((await journal.get()).data(),historical);assert.deepEqual(await x.claim(id,'no_loan_claim'),paid);
  assert.equal((await readMyDeliveries(db,x.auth,{protocolVersion:1},x.clock)).result.items.length,2);
 });
 await t.test('FINAL budget: cancellation and replacement exhaust mutation budget; bootstrap preserves old receipts',async()=>{
  const x=await setup();const old=[];for(const key of ['create','starter']){const ref=db.doc(`v2Players/${x.uid}/commands/${key}`);old.push([ref,await ref.get()]);}
  for(let i=0;i<5;i++){const q=await x.quote(`q_${i}`);await x.command('cancelCompanyLoanQuote',`cancel_${i}`,{offerId:q.result.offerId});}
  const a=(await x.anchor.get()).data();assert.equal(a.requests,10);assert.equal(a.writes,10);const before=await snap(x);
  await assert.rejects(x.quote('over_budget'),e=>e.details.code==='BANK_RATE_LIMITED');assert.deepEqual(await snap(x),before);
  assert.equal(await balance(x),100);assert.equal((await x.progression.get()).data().xp,0);
  for(const [ref,saved] of old){const current=await ref.get();assert.deepEqual(current.data(),saved.data());assert.ok(current.updateTime.isEqual(saved.updateTime));}
 });
 await t.test('FINAL pinned catalog transition and bootstrap preserve prior authority and receipts',async()=>{
  const x=await setup({noBank:true}),old=[];
  for(const path of [`v2Players/${x.uid}/commands/create`,`v2Players/${x.uid}/commands/starter`,x.finance.path,x.progression.path]){const ref=db.doc(path);old.push([ref,await ref.get()]);}
  await initializeBank(db,x.auth,req('bank'),x.version);
  for(const [ref,saved] of old){const current=await ref.get();assert.deepEqual(current.data(),saved.data());assert.ok(current.updateTime.isEqual(saved.updateTime));}
  const q=await x.quote('pinned');await x.catalog.update({chargeBps:900});let before=await snap(x);
  await assert.rejects(x.take(q.result.offerId,'changed_catalog'),e=>e.details.code==='FAILED_PRECONDITION');assert.deepEqual(await snap(x),before);await x.catalog.set(x.bank);
  // A different trusted version is not an implicit migration of this company's pinned catalog.
  const other=db.collection('v2BankCatalog').doc(`${x.version}_next`);configs.push(other);await other.set({...x.bank,version:other.id,chargeBps:900});
  assert.equal((await x.read()).result.catalogVersion,x.version);assert.equal((await x.take(q.result.offerId)).result.fee,'1');
  const generated=await generateContractOffers(db,x.auth,req('drift_pool'),{configVersion:x.version,epoch:Math.floor(x.now/600000),assets:candidates},x.clock);
  const config=db.collection('v2ContractCatalog').doc(x.version);await config.update({bankPolicy:{...x.config.bankPolicy,policy:'changed'}});before=await snap(x);
  await assert.rejects(acceptContractCommand(db,x.auth,req('drift_accept',{contractId:generated.result.contractIds[0]}),x.clock));assert.deepEqual(await snap(x),before);
  await config.set(x.config);
 });
 }finally{
  // Only roots generated by this test's emulator sign-ups; never scan/delete other companies.
  for(const root of roots)await db.recursiveDelete(root);for(const ref of configs)await ref.delete();
  for(const token of tokens)assert.equal((await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:delete?key=synthetic-key',{idToken:token})).status,200);
  await db.terminate();if(previous===undefined)delete process.env.FUNCTIONS_EMULATOR;else process.env.FUNCTIONS_EMULATOR=previous;
 }
});

import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,mkdtempSync,writeFileSync,rmSync,rmdirSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {MAX,money,display,basisPoints,add,initialState,takeLoan,settleDelivery,earlyRepay,acquirePair,affordable,assertState,metrics,simulate,liability,validateScenario,waitingBound} from './model.mjs';
import {candidate,scenarios,sensitivities} from './scenarios.mjs';
import {comparisonReport} from './report.mjs';
const root=fileURLToPath(new URL('../../../',import.meta.url)).replace(/[\\/]$/,'');
const code=(fn,c)=>assert.throws(fn,e=>e.code===c);
const advance=(s,c,n)=>{for(let i=0;i<n;i++)s=settleDelivery(s,c).state;return s;};

test('base growth reaches second truck and driver within10–15 deliveries with reserve',()=>{
 const r=simulate(candidate());assert.equal(r.summary.purchaseAt,13);assert.equal(r.summary.cashAtPurchase,money('5300'));assert.equal(r.state.acquiredPairs,1);
});
test('low/base/high profits produce16/13/11 deliveries without borrowing',()=>{
 const r=scenarios().filter(c=>c.borrowAfter===null).map(simulate);assert.deepEqual(r.map(x=>x.rows[0].profit),['2500','3100','3800'].map(money));assert.deepEqual(r.map(x=>x.summary.purchaseAt),[16,13,11]);
 assert.deepEqual(r.map(x=>x.summary.cashAtPurchase),['5000','5300','6800'].map(money));
});
test('credit at fifth is earliest authorized and starts repayment only at sixth',()=>{
 code(()=>simulate(candidate({borrowAfter:4})),'LOAN_LOCKED');const c=candidate({borrowAfter:5}),r=simulate(c);
 assert.equal(r.rows[4].disbursement,money('20000'));assert.equal(r.rows[4].repayment,0);assert.equal(r.rows[5].repayment,money('1085'));assert.equal(r.summary.purchaseAt,8);
 assert.deepEqual(simulate(candidate({borrowAfter:c.loan.unlockDeliveries})),r);
});
test('loan payout changes cash and liability together and leaves prior immutable state intact',()=>{
 const c=candidate({autoAcquire:false}),before=advance(initialState(c),c,5),saved=structuredClone(before),s=takeLoan(before,c);
 assert.deepEqual(before,saved);assert.equal(s.cash-before.cash,money('20000'));assert.equal(liability(s),money('21600'));assert.equal(s.financeCharges,money('1600'));
});
test('8 percent fixed charge uses exact flooring, no passive accrual',()=>{
 assert.equal(basisPoints(money('20000'),800),money('1600'));assert.equal(basisPoints(1,800),0);
 const c=candidate({loan:{principal:100,chargeBps:800,unlockDeliveries:0,repaymentBps:0}});let s=takeLoan(initialState(c),c);s=advance(s,c,10);assert.equal(liability(s),108);assert.equal(s.financeCharges,8);
});
test('35 percent automatic repayment and fee/principal allocation are distinct from expenses',()=>{
 const c=candidate(),s=takeLoan(advance(initialState(c),c,5),c),r=settleDelivery(s,c);
 assert.equal(r.profit,money('3100'));assert.equal(r.repayment,money('1085'));assert.equal(r.feeRepayment,money('1085'));assert.equal(r.principalRepayment,0);
 const next=settleDelivery(r.state,c);assert.equal(next.feeRepayment,money('515'));assert.equal(next.principalRepayment,money('570'));assert.equal(next.expenses,money('1400'));
});
test('zero or negative operating profit never repays debt',()=>{
 for(const gross of ['1400','1000']){const c=candidate({gross:money(gross),loan:{principal:money('20000'),chargeBps:800,unlockDeliveries:0,repaymentBps:3500}}),s=takeLoan(initialState(c),c),r=settleDelivery(s,c);assert.equal(r.repayment,0);assert.equal(liability(r.state),liability(s));}
});
test('reserve caps repayment to available cash and does not invent money below floor',()=>{
 for(const startingCash of [0,money('1900')]){const c=candidate({startingCash,costTiming:'net_completion',loan:{principal:money('100'),chargeBps:800,unlockDeliveries:0,repaymentBps:3500}}),s=takeLoan(initialState(c),c),r=settleDelivery(s,c);
 assert.equal(r.repayment,startingCash===0?0:money('100'));assert.equal(r.state.cash,startingCash===0?money('3200'):money('5000'));}
});
test('payments never exceed debt, payoff stops deductions at30/25/22',()=>{
 for(const [index,r] of scenarios().filter(c=>c.borrowAfter!==null).map(simulate).entries()){
  assert.equal(r.summary.payoffAt,[30,25,22][index]);let debt=0;for(const row of r.rows){debt=add(debt,row.disbursement,row.charge);assert.ok(row.repayment<=debt);debt-=row.repayment;assert.equal(debt,row.debt);}
  assert.equal(r.rows.slice(r.summary.payoffAt).reduce((n,x)=>n+x.repayment,0),0);
 }
});
test('early repayment is capped to liability and reserve, no extra charge or refund',()=>{
 const c=candidate({startingCash:money('100000'),autoAcquire:false,loan:{principal:money('20000'),chargeBps:800,unlockDeliveries:0,repaymentBps:3500}}),s=takeLoan(initialState(c),c),r=earlyRepay(s,c,MAX);
 assert.equal(liability(r),0);assert.equal(r.cash,money('98400'));assert.equal(r.financeCharges,money('1600'));assert.equal(r.loan.status,'paid');assert.equal(r.revenue,0);code(()=>earlyRepay(r,c,1),'NO_ACTIVE_LOAN');
 const small=candidate({startingCash:0,loan:{...c.loan,principal:money('100')}}),low=takeLoan(initialState(small),small);assert.deepEqual(earlyRepay(low,small,MAX),low);
});
test('repeat loan application and automatic reborrowing after payoff are rejected',()=>{
 const c=candidate({startingCash:money('100000'),autoAcquire:false,loan:{principal:money('20000'),chargeBps:800,unlockDeliveries:0,repaymentBps:3500}}),s=takeLoan(initialState(c),c);
 code(()=>takeLoan(s,c),'REBORROW_NOT_AUTHORIZED');code(()=>takeLoan(earlyRepay(s,c,MAX),c),'REBORROW_NOT_AUTHORIZED');assert.equal(simulate(candidate({borrowAfter:5,deliveries:100})).state.principalReceived,money('20000'));
});
test('borrowing is not operating income, completion, XP or realized company profit',()=>{
 const c=candidate(),before=advance(initialState(c),c,5),s=takeLoan(before,c);
 for(const key of ['revenue','expenses','operatingProfit','completed'])assert.equal(s[key],before[key]);assert.equal('xp' in s,false);
 assert.equal(metrics(s).realizedCompanyProfit,metrics(before).realizedCompanyProfit);assert.equal(metrics(s).netFinancialPosition,metrics(before).netFinancialPosition-money('1600'));
});
test('principal repayment reduces cash and liability equally without double expensing',()=>{
 const c=candidate({autoAcquire:false}),s=takeLoan(advance(initialState(c),c,5),c),r=earlyRepay(s,c,money('2000'));
 assert.equal(s.cash-r.cash,money('2000'));assert.equal(liability(s)-liability(r),money('2000'));assert.equal(r.expenses,s.expenses);assert.equal(r.operatingProfit,s.operatingProfit);
 assert.equal(metrics(r).realizedCompanyProfit,s.operatingProfit-money('1600'));assert.equal(metrics(r).netFinancialPosition,metrics(s).netFinancialPosition);
});
test('exact affordability includes truck, driver and reserve; expansion is capital spending',()=>{
 const c=candidate({startingCash:money('55000'),autoAcquire:false}),s=initialState(c);assert.equal(affordable(s,c),true);const r=acquirePair(s,c);assert.equal(r.cash,money('5000'));assert.equal(r.expenses,0);assert.equal(r.capitalSpent,money('50000'));
 const low=candidate({startingCash:money('54999.99')});code(()=>acquirePair(initialState(low),low),'ACQUISITION_UNAFFORDABLE');
});
test('malformed, fractional, negative, unknown and inconsistent scenario inputs fail explicitly',()=>{
 for(const change of [{startingCash:-1},{gross:0.1},{truckCost:0},{currency:'USD'},{scale:3},{approval:'production'},
  {deliveries:0},{deliveries:10001},{extra:1},{costs:{fuel:1}},{loan:{principal:1,chargeBps:10001,unlockDeliveries:5,repaymentBps:3500}},{borrowAfter:31}])assert.throws(()=>validateScenario(candidate(change)));
});
test('money parsing and products do not hide floating point rounding',()=>{
 assert.equal(money('15000.00'),1500000);assert.equal(money('0.01'),1);assert.equal(display(money('90071992547409.91')),'90071992547409.91');assert.equal(basisPoints(MAX,10000),MAX);assert.equal(basisPoints(101,3500),35);
 for(const input of ['0.001','1e3','-1','NaN',1.1])assert.throws(()=>money(input));
});
test('unsafe additions and aggregate totals reject overflow atomically',()=>{
 code(()=>add(MAX,1),'OVERFLOW');code(()=>money('90071992547409.92'),'OVERFLOW');assert.throws(()=>validateScenario(candidate({costs:{fuel:MAX,wages:1,maintenance:0,tolls:0}})));
 const c=candidate({startingCash:MAX,autoAcquire:false,loan:{principal:1,chargeBps:0,unlockDeliveries:0,repaymentBps:0}}),s=initialState(c),saved=structuredClone(s);code(()=>takeLoan(s,c),'OVERFLOW');assert.deepEqual(s,saved);code(()=>settleDelivery(s,c),'OVERFLOW');assert.deepEqual(s,saved);
});
test('very low profit misses target; loss and upfront shortage stop without bailout',()=>{
 assert.equal(simulate(candidate({gross:money('1500')})).summary.purchaseAt,null);
 const loss=simulate(candidate({gross:money('1000'),deliveries:100}));assert.equal(loss.blocked.reason,'INSUFFICIENT_OPERATING_CASH');assert.equal(loss.state.cash,money('600'));assert.equal(loss.state.completed,36);
 const noCash=simulate(candidate({startingCash:0}));assert.equal(noCash.rows.length,0);assert.equal(noCash.state.cash,0);assert.equal(noCash.state.principalReceived,0);
});
test('completion shortage is rejected even without upfront charging',()=>{
 const c=candidate({startingCash:0,gross:0,costTiming:'net_completion'}),s=initialState(c),before=structuredClone(s);code(()=>settleDelivery(s,c),'INSUFFICIENT_SETTLEMENT_CASH');assert.deepEqual(s,before);
});
test('fuel, wage, maintenance and combined shocks produce explicit slower growth',()=>{
 const r=Object.fromEntries(sensitivities().map(c=>[c.name,simulate(c).summary.purchaseAt]));assert.equal(r['fuel-plus-50'],15);assert.equal(r['wages-plus-50'],14);assert.equal(r['maintenance-plus-50'],14);assert.equal(r['combined-shock'],17);
});
test('one extra fleet does not double income with one offer per wave',()=>{
 const plain=simulate(candidate()),credit=simulate(candidate({borrowAfter:5}));assert.equal(waitingBound(plain,1,60).minutes,1800);assert.equal(waitingBound(credit,1,60).minutes,1800);
 assert.equal(waitingBound(plain,2,60).minutes,1320);assert.equal(waitingBound(credit,2,60).minutes,1140);
});
test('every deterministic row conserves cash and debt; final asset-inclusive position matches committed-charge profit',()=>{
 for(const input of [...scenarios(),...sensitivities()]){const r=simulate(input);assertState(r.state);let cash=r.opening.cash,debt=r.opening.debt;
  for(const row of r.rows){cash=add(cash,row.profit,row.disbursement,-row.repayment,row.purchased?-add(input.truckCost,input.driverCost):0);debt=add(debt,row.disbursement,row.charge,-row.repayment);assert.equal(cash,row.cash);assert.equal(debt,row.debt);}
  assert.equal(r.summary.financialPositionPlusAcquiredCost,add(input.startingCash,r.summary.profitAfterCommittedCharge));
 }
});
test('corrupt loan, negative wallet or inconsistent model state is not repaired',()=>{
 const c=candidate(),s=initialState(c);for(const change of [{cash:-1},{cash:s.cash+1},{everBorrowed:true},{completed:0.5}])assert.throws(()=>assertState({...s,...change}));
});
test('same inputs produce byte-identical results and checked-in report',()=>{
 assert.equal(JSON.stringify(scenarios().map(simulate)),JSON.stringify(scenarios().map(simulate)));const report=comparisonReport();assert.equal(comparisonReport(),report);
 assert.equal(readFileSync(join(root,'docs/backend/ECONOMY-00_SIMULATION_REPORT.md'),'utf8').replace(/\r\n/g,'\n'),report);
});
test('CLI accepts local JSON scenarios and rejects malformed usage without network',()=>{
 const temp=mkdtempSync(join(tmpdir(),'logisticore-economy-00-'));
 try{const file=join(temp,'input.json');writeFileSync(file,JSON.stringify(candidate()));const script=fileURLToPath(new URL('./run.mjs',import.meta.url));
 const good=spawnSync(process.execPath,[script,'--input',file],{encoding:'utf8'});assert.equal(good.status,0,good.stderr);assert.equal(JSON.parse(good.stdout)[0].summary.purchaseAt,13);
 const bad=spawnSync(process.execPath,[script,'--deploy'],{encoding:'utf8'});assert.equal(bad.status,1);
 }finally{rmSync(join(temp,'input.json'),{force:true});rmdirSync(temp);}
});
// The design-only runtime freeze ended with BACKEND-09A. Keep infrastructure
// protected without incorrectly freezing every later authorized implementation.
test('dependency manifests, isolation infrastructure and Unity files match reviewed base',()=>{
 const result=spawnSync('git',['-c',`safe.directory=${root.replace(/\\/g,'/')}`,'diff','--name-only','186f8bcc203c5e39409ae4818e1b1d47938c352d','--',
 'backend/functions/package.json','backend/functions/src/localEnvironment.ts','backend/scripts','backend/package.json','backend/firebase.json','backend/firestore.rules','backend/firestore.indexes.json','Assets','Packages','ProjectSettings'],{cwd:root,encoding:'utf8'});
 assert.equal(result.status,0,result.stderr);assert.equal(result.stdout.trim(),'');
 // SECURITY-02 approved four-package refresh; normalize only checkout line endings.
 const lock=readFileSync(join(root,'backend/package-lock.json'),'utf8').replace(/\r\n/g,'\n');
 assert.equal(createHash('sha256').update(lock).digest('hex'),'14d593562413c39bfaff9e74b7490c900ae33e570770cd049e2c77437002ab4a');
});

test('immutable model config rejects altered repayment terms after loan origination',()=>{
 const c=candidate(),s=takeLoan(advance(initialState(c),c,5),c),changed={...c,loan:{...c.loan,repaymentBps:0}};
 code(()=>settleDelivery(s,changed),'CONFIG_DRIFT');code(()=>earlyRepay(s,changed,1),'CONFIG_DRIFT');
});

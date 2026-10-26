import test from 'node:test';
import assert from 'node:assert/strict';
import {catalog,simulate,scenarios} from './model.mjs';
import {marketSnapshot,marketQuote} from '../../functions/lib/marketModel.js';
test('offline market equal inputs produce byte-identical results without randomness',()=>{assert.deepEqual(scenarios(),scenarios());});
test('neutral acquisition baseline13 matches historical comparison and extremes are not universally10-15',()=>{
 const rows=scenarios();assert.equal(rows.find(r=>r.name==='neutral-base').acquiredAt,13);assert.equal(rows.find(r=>r.name==='low-profit-costs').acquiredAt,16);assert.equal(rows.find(r=>r.name==='high-profit-costs').acquiredAt,11);
 assert.equal(rows.find(r=>r.name==='high-demand').acquiredAt,11);assert.equal(rows.find(r=>r.name==='high-supply').acquiredAt,16);
});
test('credit adds principal and fee debt without free revenue; full-period conservation includes capital',()=>{
 for(const r of scenarios()){
  const starting=r.name.includes('zero-starting')||r.name.includes('tiny-profit')?0:1500000;
  assert.equal(r.cash+r.capital-r.debt,starting+r.profitTotal-r.fees);
  assert.equal(r.principalReceived+r.fees-r.principalPaid-r.feePaid,r.debt);
 }
});
test('market downturn with debt pins fees and never takes more than total liability',()=>{
 const r=simulate({credit:true,downturn:true});assert.equal(r.fees,160000);assert.ok(r.principalPaid+r.feePaid<=2160000);assert.ok(r.traces.slice(5).every(t=>t.adjustmentBps===-1000));
});
test('city and cargo sensitivity differ without player-specific state',()=>{
 const c=catalog({regional:true});assert.notEqual(marketSnapshot(c,0,'synthetic_city','synthetic_cargo').adjustmentBps,marketSnapshot(c,0,'synthetic_destination','synthetic_cargo').adjustmentBps);
 const m=catalog({demand:80,supply:20});assert.equal(marketSnapshot(m,0,'synthetic_destination','synthetic_secondary').adjustmentBps,750);
});
test('epoch changes are deterministic and bounded across100 periods',()=>{
 const c=catalog({jitter:50}),rows=Array.from({length:100},(_,epoch)=>marketQuote(c,epoch,'synthetic_city','synthetic_cargo',450000));
 assert.ok(new Set(rows.map(r=>r.adjustedGrossReward)).size>1);assert.ok(rows.every(r=>r.adjustedGrossReward>=405000&&r.adjustedGrossReward<=517500));
});
test('unprofitable offers never write money and debt can remain indefinitely under missing supply',()=>{
 for(const options of [{costs:600000},{offersPerPeriod:0}]){const r=simulate({credit:true,...options});assert.equal(r.deliveries,0);assert.equal(r.cash,3500000);assert.equal(r.debt,2160000);assert.equal(r.acquiredAt,null);}
});
test('additional fleet only accelerates earnings under sufficient offer supply',()=>{
 const one=simulate(),two=simulate({offersPerPeriod:2});assert.equal(one.acquiredAt,two.acquiredAt);assert.ok(two.deliveries>one.deliveries);assert.ok(two.cash>one.cash);
});
test('near-zero profit does not erase debt or provide automatic rescue',()=>{const r=simulate({startingCash:0,credit:true,costs:449999});assert.equal(r.debt,2160000);assert.equal(r.acquiredAt,null);assert.equal(r.cash,2000030);});
test('simulation bounds and actual final wallet cap fail without floating arithmetic',()=>{for(const options of [{periods:101},{offersPerPeriod:3},{costs:-1},{startingCash:Number.MAX_SAFE_INTEGER},{maxBalance:1500000}])assert.throws(()=>simulate(options));});

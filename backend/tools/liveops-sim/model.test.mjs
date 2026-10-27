import test from 'node:test';
import assert from 'node:assert/strict';
import {event,world,quote,simulate,scenarios} from './model.mjs';
test('LiveOps simulation24 scenarios are deterministic and preserve all financial value',()=>{
 const rows=scenarios();assert.equal(rows.length,24);assert.deepEqual(rows,scenarios());for(const r of rows){const start=r.name==='low-cash-tiny-profit'?0:1500000;assert.equal(r.cash+r.capital-r.debt,start+r.profitTotal-r.fees);assert.ok(r.paid<=2160000);}
});
test('exact event boundaries and immutable delayed completion never reprice',()=>{
 const w=world({events:[event({startsAt:1000,endsAt:2000})]});assert.equal(quote(w,999).finalGrossReward,450000);assert.equal(quote(w,1000).finalGrossReward,495000);assert.equal(quote(w,2000).finalGrossReward,450000);
 const r=simulate({events:[event({endsAt:60000})],duration:3});assert.equal(r.traces[0].completedAt,180000);assert.equal(r.traces[0].finalGross,495000);assert.equal(r.traces[1].finalGross,450000);
});
test('overlap and tie ordering give only one modifier, never stacked rewards',()=>{
 const events=[event({eventId:'z'}),event({eventId:'a',eventBps:500})],q=quote(world({events}),0);assert.equal(q.selectedEvent.eventId,'a');assert.equal(q.finalGrossReward,472500);assert.equal(quote(world({events:[...events].reverse()}),0).finalGrossReward,q.finalGrossReward);
});
test('city and cargo scope do not create unrelated price bonuses',()=>{
 const w=world({events:[event()]});assert.equal(quote(w,0,{city:'synthetic_city'}).finalGrossReward,450000);assert.equal(quote(w,0,{cargo:'synthetic_secondary'}).finalGrossReward,450000);assert.equal(quote(w,0).finalGrossReward,495000);
});
test('combined caps bound inflation despite market and event extremes',()=>{
 const rows=scenarios();for(const r of rows)assert.ok(r.traces.every(t=>t.combinedBps>=-1000&&t.combinedBps<=1500));assert.equal(rows.find(r=>r.name==='maximum-cap').traces[0].finalGross,517500);assert.equal(rows.find(r=>r.name==='minimum-cap').traces[0].finalGross,405000);
});
test('second-fleet target is a benchmark; negative events and long durations change growth',()=>{
 const rows=scenarios();assert.equal(rows.find(r=>r.name==='no-event').acquiredAt,13);assert.equal(rows.find(r=>r.name==='positive').acquiredAt,12);assert.equal(rows.find(r=>r.name==='negative').acquiredAt,16);
 assert.ok(rows.find(r=>r.name==='two-offers-short').deliveries>rows.find(r=>r.name==='two-offers-long').deliveries);
});
test('debt is never free money or silently forgiven under no supply and integer rounding',()=>{
 const rows=scenarios();for(const name of ['zero-offers-debt','low-cash-tiny-profit'])assert.equal(rows.find(r=>r.name===name).debt,2160000);assert.equal(rows.find(r=>r.name==='unprofitable').deliveries,0);
});
test('untrusted event magnitude and unsafe bounds reject instead of inflating cash',()=>{
 assert.throws(()=>world({events:[event({eventBps:1501})]}));assert.throws(()=>world({events:[event(),event()]}));for(const options of [{offers:3},{duration:0},{periods:101},{costs:-1},{startingCash:Number.MAX_SAFE_INTEGER}])assert.throws(()=>simulate(options));
});

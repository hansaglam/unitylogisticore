import test from 'node:test';
import assert from 'node:assert/strict';
import {marketFixtures} from './market.fixtures.mjs';
import {validateMarketCatalog,marketSnapshot,marketEpoch,marketQuote,validateMarketQuote,adjustedReward} from '../functions/lib/marketModel.js';
import {digest} from '../functions/lib/logisticsModel.js';
import {validateContractConfig,makeTerms,validateContract,contractIdFor} from '../functions/lib/contractModel.js';
import {Timestamp} from 'firebase-admin/firestore';
import {candidates} from './contracts.fixtures.mjs';
const fixture=marketFixtures('market_unit');
const validate=(raw=fixture.market)=>validateMarketCatalog(raw,fixture.market.version,fixture.logistics,fixture.economy,fixture.config.cargo.map(c=>c.id));
test('market strict catalog rejects missing/extra fields, cities/cargo, duplicates, unsafe bounds and pins',()=>{
 assert.equal(digest(validate()),digest(fixture.market));const missing={...fixture.market};delete missing.seed;
 assert.throws(()=>validateMarketCatalog(undefined,fixture.market.version,fixture.logistics,fixture.economy,['synthetic_cargo']));
 for(const raw of [missing,...[{extra:1},{approval:'production'},{version:'other'},{currency:'USD'},{scale:3},{economyHash:'bad'},{logisticsHash:'bad'},
  {policyHash:'bad'},{policy:{...fixture.market.policy,rounding:'random'}},{periodMs:0},{periodMs:604800001},{indexMax:-1},{jitter:101},{sensitivityBps:1.5},{maxGrossReward:10001},
  {minAdjustmentBps:-1001},{maxAdjustmentBps:1501},{seed:'../x'},{regions:[]},{cargo:[]},
  {regions:[{...fixture.market.regions[0],cityId:'unsupported'}]},{cargo:[{cargoId:'unsupported',weightBps:10000}]},
  {regions:[fixture.market.regions[0],fixture.market.regions[0]]},{cargo:[fixture.market.cargo[0],fixture.market.cargo[0]]},
  {regions:[{...fixture.market.regions[0],demand:Number.MAX_SAFE_INTEGER}]},{cargo:[{...fixture.market.cargo[0],weightBps:-1}]}].map(x=>({...fixture.market,...x}))])assert.throws(()=>validate(raw));
});
test('market deterministic epochs and bounded indicators use no random transaction behavior',()=>{
 const c=validate({...fixture.market,jitter:10});const original=Math.random;Math.random=()=>{throw Error('random forbidden');};
 try{assert.deepEqual(marketSnapshot(c,7,'synthetic_city','synthetic_cargo'),marketSnapshot(c,7,'synthetic_city','synthetic_cargo'));
  const rows=Array.from({length:100},(_,epoch)=>marketSnapshot(c,epoch,'synthetic_city','synthetic_cargo'));
  assert.ok(new Set(rows.map(r=>r.adjustmentBps)).size>1);assert.ok(rows.every(r=>r.demandIndex>=0&&r.demandIndex<=100&&r.adjustmentBps>=-1000&&r.adjustmentBps<=1500));
 }finally{Math.random=original;}
 assert.equal(marketEpoch(59999,60000),0);assert.equal(marketEpoch(60000,60000),1);assert.throws(()=>marketEpoch(-1,60000));
 assert.throws(()=>marketSnapshot(c,Number.MAX_SAFE_INTEGER,'synthetic_city','synthetic_cargo'));assert.throws(()=>marketSnapshot(c,0,'foreign','synthetic_cargo'));
});
test('market pressure sign, regional/cargo weights and min/max caps preserve neutral gross',()=>{
 for(const [demand,supply,bps,gross] of [[50,50,0,100],[100,0,1500,115],[0,100,-1000,90]]){
  const c=validate({...fixture.market,regions:fixture.market.regions.map(r=>({...r,demand,supply}))});
  const q=marketQuote(c,0,'synthetic_city','synthetic_cargo',100);assert.equal(q.snapshot.adjustmentBps,bps);assert.equal(q.adjustedGrossReward,gross);
 }
 const c=validate({...fixture.market,regions:fixture.market.regions.map((r,i)=>({...r,demand:60,supply:50,weightBps:i?5000:10000})),cargo:[{cargoId:'synthetic_cargo',weightBps:5000}]});
 assert.equal(marketSnapshot(c,0,'synthetic_city','synthetic_cargo').adjustmentBps,125);assert.equal(marketSnapshot(c,0,'synthetic_destination','synthetic_cargo').adjustmentBps,62);
});
test('market monetary floor, overflow and exact snapshot corruption fail closed',()=>{
 assert.equal(adjustedReward(101,1500,1000),116);assert.equal(adjustedReward(101,-1000,1000),90);
 for(const args of [[0,0,100],[-1,0,100],[1,0.1,100],[Number.MAX_SAFE_INTEGER,1500,Number.MAX_SAFE_INTEGER],[100,1501,1000],[1,-1000,10]])assert.throws(()=>adjustedReward(...args));
 const q=marketQuote(validate(),0,'synthetic_city','synthetic_cargo',100);validateMarketQuote(q,100,'synthetic_city','synthetic_cargo');
 for(const patch of [{adjustedGrossReward:101},{pricingVersion:3},{snapshot:{...q.snapshot,epoch:1}},{snapshot:{...q.snapshot,policyHash:'bad'}},{snapshot:null}])assert.throws(()=>validateMarketQuote({...q,...patch},100,'synthetic_city','synthetic_cargo'));
});
test('market V4 pins adjusted gross and V3 bank policy, rejects unprofitable pool and missing snapshot',()=>{
 const f=marketFixtures('market_v4',{demand:0,supply:100});const c=validateContractConfig(f.config,f.config.version,f.logistics,f.economy);
 const m=validateMarketCatalog(f.market,f.market.version,f.logistics,f.economy,['synthetic_cargo']);const terms=makeTerms(c,0,candidates,f.economy,f.costCatalog,m,0);
 assert.equal(terms.reward,90);assert.equal(terms.settlement.grossReward,90);assert.equal(terms.settlement.netReward,86);assert.equal(terms.bankPolicy.settlementVersion,3);
 const expensive={...f.costCatalog,entries:f.costCatalog.entries.map(e=>({...e,costs:{fuelCost:90,driverCost:0,maintenanceCost:0,tollCost:0}}))};
 assert.throws(()=>makeTerms({...c,costCatalogHash:digest(expensive)},0,candidates,f.economy,expensive,m,0));
 const companyId='synthetic',id=contractIdFor(companyId,0,0),record={schemaVersion:4,companyId,contractId:id,status:'offered',epoch:0,slot:0,terms,termsHash:digest(terms),createdAt:Timestamp.fromMillis(1),expiresAt:Timestamp.fromMillis(100000)};
 validateContract(record,id,companyId);const bad={...terms};delete bad.market;assert.throws(()=>validateContract({...record,terms:bad,termsHash:digest(bad)},id,companyId));
 assert.throws(()=>validateContract({...record,createdAt:Timestamp.fromMillis(60000)},id,companyId));
});
test('market read and pinned validator reject nonlocal environment before any database access',async()=>{
 const {readMyMarketState,readPinnedMarket}=await import('../functions/lib/market.js');const previous=process.env.GCLOUD_PROJECT;process.env.GCLOUD_PROJECT='synthetic-nonlocal';
 const db=new Proxy({}, {get(){throw Error('database accessed');}});
 try{await assert.rejects(readMyMarketState(db,{uid:'test'},{protocolVersion:1}),/Local isolation/);await assert.rejects(readPinnedMarket(db,db,db,db),/Local isolation/);}
 finally{if(previous===undefined)delete process.env.GCLOUD_PROJECT;else process.env.GCLOUD_PROJECT=previous;}
});

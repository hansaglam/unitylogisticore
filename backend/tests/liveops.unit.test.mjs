import test from 'node:test';
import assert from 'node:assert/strict';
import {Timestamp} from 'firebase-admin/firestore';
import {liveOpsFixtures,syntheticEvent} from './liveops.fixtures.mjs';
import {marketFixtures} from './market.fixtures.mjs';
import {validateLiveOpsCatalog,selectEvent,eventStatus,liveOpsQuote,validateLiveOpsQuote} from '../functions/lib/liveOpsModel.js';
import {marketQuote} from '../functions/lib/marketModel.js';
import {validateContractConfig,makeTerms,validateContract,contractIdFor} from '../functions/lib/contractModel.js';
import {digest} from '../functions/lib/logisticsModel.js';
import {candidates} from './contracts.fixtures.mjs';
const f=liveOpsFixtures('liveops_unit',{eventBps:1000}),validate=raw=>validateLiveOpsCatalog(raw,f.liveOps.version,f.economy,f.logistics,f.market);
test('LiveOps strict catalog schema, pins, bounds, localization and event definitions fail closed',()=>{
 assert.equal(digest(validate(f.liveOps)),digest(f.liveOps));
 const missing={...f.liveOps};delete missing.events;for(const raw of [undefined,missing,...[{extra:1},{schemaVersion:2},{marketHash:'bad'},{economyHash:'bad'},{logisticsHash:'bad'},
  {currency:'USD'},{scale:3},{version:'foreign'},{policyHash:'bad'},{policy:{...f.liveOps.policy,maxEffectiveEvents:2}},{maxEvents:17},{maxEvents:0},{minCombinedBps:-1001},{maxCombinedBps:1501},{minCombinedBps:-500},
  {events:[f.liveOps.events[0],f.liveOps.events[0]]}].map(p=>({...f.liveOps,...p}))])assert.throws(()=>validate(raw));
 for(const patch of [{type:'weather'},{titleKey:'Hard coded text'},{enabled:1},{priority:-1},{priority:1001},{adjustmentBps:1501},{adjustmentBps:0.5},
  {startsAt:-1},{startsAt:30000},{endsAt:0},{endsAt:2678400001},{endsAt:Number.MAX_SAFE_INTEGER},{cityIds:['foreign']},{cargoIds:['foreign']},
  {cityIds:[]},{cargoIds:['synthetic_cargo','synthetic_cargo']},{type:'logistics_congestion',adjustmentBps:1},{type:'regional_demand_surge',adjustmentBps:-1},{extra:1}])assert.throws(()=>validate({...f.liveOps,events:[{...f.liveOps.events[0],...patch}]}));
});
test('LiveOps half-open intervals, disabled entries and city/cargo applicability are deterministic',()=>{
 const e=f.liveOps.events[0];assert.equal(eventStatus(e,0),'active');assert.equal(eventStatus(e,29999),'active');assert.equal(eventStatus(e,30000),'ended');
 assert.equal(eventStatus({...e,startsAt:1},0),'upcoming');assert.equal(selectEvent(f.liveOps,0,'synthetic_destination','synthetic_cargo').eventId,e.eventId);
 assert.equal(selectEvent(f.liveOps,0,'synthetic_city','synthetic_cargo'),null);assert.equal(selectEvent(f.liveOps,0,'synthetic_destination','foreign'),null);
 assert.equal(selectEvent({...f.liveOps,events:[{...e,enabled:false}]},0,'synthetic_destination','synthetic_cargo'),null);
});
test('LiveOps overlap chooses one highest priority then ASCII eventId independent of input ordering',()=>{
 const events=[syntheticEvent({eventId:'z',eventBps:1000,priority:2}),syntheticEvent({eventId:'a',eventBps:-500,priority:2}),syntheticEvent({eventId:'b',eventBps:1500,priority:1})];
 for(const es of [events,[...events].reverse()]){const c=validate({...f.liveOps,events:es}),m=marketQuote(f.market,0,'synthetic_destination','synthetic_cargo',100),q=liveOpsQuote(c,m,1,10000);assert.equal(q.selectedEvent.eventId,'a');assert.equal(q.finalGrossReward,95);assert.equal(q.eventAdjustmentBps,-500);}
});
test('LiveOps composes bps once from base, floors BigInt and respects both combined caps',()=>{
 for(const [marketBps,eventBps,expected] of [[1500,1000,116],[-1000,-500,90],[250,250,106],[0,1000,111]]){
  const market={...f.market,sensitivityBps:25,regions:f.market.regions.map(r=>({...r,demand:marketBps===1500?100:marketBps===-1000?0:marketBps===250?60:50,supply:marketBps===1500?0:marketBps===-1000?100:50}))};
  const c={...f.liveOps,marketHash:digest(market),events:[syntheticEvent({eventBps})]},m=marketQuote(market,0,'synthetic_destination','synthetic_cargo',101),q=liveOpsQuote(c,m,1,10000);
  assert.equal(q.finalGrossReward,expected);assert.deepEqual(liveOpsQuote(c,m,1,10000),q);assert.equal(q.marketGrossReward,m.adjustedGrossReward);
 }
 const m=marketQuote(f.market,0,'synthetic_destination','synthetic_cargo',100);assert.throws(()=>liveOpsQuote(f.liveOps,m,1,109));assert.throws(()=>liveOpsQuote(f.liveOps,m,60000,10000));
 const huge={...m,baseGrossReward:Number.MAX_SAFE_INTEGER,adjustedGrossReward:Number.MAX_SAFE_INTEGER};assert.throws(()=>liveOpsQuote(f.liveOps,huge,1,Number.MAX_SAFE_INTEGER));
});
test('LiveOps snapshot binds selected/no-event evidence and detects arithmetic/hash/time corruption',()=>{
 const m=marketQuote(f.market,0,'synthetic_destination','synthetic_cargo',100),q=liveOpsQuote(f.liveOps,m,1,10000);validateLiveOpsQuote(q,m,110,1);
 for(const patch of [{selectedEvent:null},{selectedAt:2},{catalogHash:'bad'},{finalGrossReward:111},{eventAdjustmentBps:0},{marketGrossReward:110},{combinedAdjustmentBps:1500},{policyVersion:2}])assert.throws(()=>validateLiveOpsQuote({...q,...patch},m,110,1));
 const no=liveOpsQuote(f.liveOps,m,30000,10000);assert.equal(no.selectedEvent,null);assert.equal(no.finalGrossReward,100);validateLiveOpsQuote(no,m,100,30000);
 const {snapshotHash,...body}=no;const wrong={...body,eventAdjustmentBps:1};assert.throws(()=>validateLiveOpsQuote({...wrong,snapshotHash:digest(wrong)},m,100,30000));
});
test('V5 validates market-only and final gross separately; V4 mismatch remains rejected and costs use final gross',()=>{
 const c=validateContractConfig(f.config,f.config.version,f.logistics,f.economy),t=makeTerms(c,0,candidates,f.economy,f.costCatalog,f.market,0,validate(f.liveOps),1);
 assert.equal(t.reward,110);assert.equal(t.market.adjustedGrossReward,100);assert.equal(t.settlement.grossReward,110);assert.equal(t.settlement.netReward,106);assert.equal(t.bankPolicy.settlementVersion,3);
 const cid='synthetic',id=contractIdFor(cid,0,0),record={schemaVersion:5,companyId:cid,contractId:id,status:'offered',epoch:0,slot:0,terms:t,termsHash:digest(t),createdAt:Timestamp.fromMillis(1),expiresAt:Timestamp.fromMillis(600000)};
 validateContract(record,id,cid);for(const patch of [{reward:100},{liveOps:null},{settlement:{...t.settlement,grossReward:100}},{market:{...t.market,adjustedGrossReward:110}}]){const bad={...t,...patch};assert.throws(()=>validateContract({...record,terms:bad,termsHash:digest(bad)},id,cid));}
 const old=marketFixtures('old_v4'),oc=validateContractConfig(old.config,old.config.version,old.logistics,old.economy),ot=makeTerms(oc,0,candidates,old.economy,old.costCatalog,old.market,0);
 assert.equal(ot.reward,100);assert.equal(Object.hasOwn(ot,'liveOps'),false);const wrong={...ot,reward:110};assert.throws(()=>validateContract({...record,schemaVersion:4,terms:wrong,termsHash:digest(wrong)},id,cid));
 const costs={...f.costCatalog,entries:f.costCatalog.entries.map(e=>({...e,costs:{fuelCost:110,driverCost:0,maintenanceCost:0,tollCost:0}}))};assert.throws(()=>makeTerms({...c,costCatalogHash:digest(costs)},0,candidates,f.economy,costs,f.market,0,validate(f.liveOps),1));
});
test('LiveOps read guard rejects nonlocal use before database access',async()=>{
 const {readMyWorldEvents}=await import('../functions/lib/liveOps.js'),old=process.env.GCLOUD_PROJECT;process.env.GCLOUD_PROJECT='synthetic-nonlocal';
 try{await assert.rejects(readMyWorldEvents(new Proxy({},{get(){throw Error('database touched');}}),{uid:'test'},{protocolVersion:1}),/Local isolation/);}finally{if(old===undefined)delete process.env.GCLOUD_PROJECT;else process.env.GCLOUD_PROJECT=old;}
});

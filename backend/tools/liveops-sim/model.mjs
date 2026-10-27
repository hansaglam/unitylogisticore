import {liveOpsFixtures,syntheticEvent} from '../../tests/liveops.fixtures.mjs';
import {catalog as marketCatalog} from '../market-sim/model.mjs';
import {validateLiveOpsCatalog,liveOpsQuote} from '../../functions/lib/liveOpsModel.js';
import {marketQuote,marketEpoch} from '../../functions/lib/marketModel.js';
import {configHash,checkedAdd,safe} from '../../functions/lib/economyModel.js';
import {digest} from '../../functions/lib/logisticsModel.js';
import {repayment} from '../../functions/lib/bankModel.js';
export const event=(patch={})=>syntheticEvent({endsAt:1800000,eventBps:1000,...patch});
export function world({events=[],market=marketCatalog()}={}){
 const f=liveOpsFixtures('market_simulation'),economy={...f.economy,maxBalance:10000000000,startingBalance:1500000};
 const liveOps=validateLiveOpsCatalog({...f.liveOps,economyHash:configHash(economy),marketHash:digest(market),events},f.liveOps.version,economy,f.logistics,market);
 return {market,liveOps,economy};
}
export function quote(w,now,{city='synthetic_destination',cargo='synthetic_cargo',base=450000}={}){
 return liveOpsQuote(w.liveOps,marketQuote(w.market,marketEpoch(now,w.market.periodMs),city,cargo,base),now,w.economy.maxBalance);
}
/** One canonical cash balance; fixed synthetic fee/debt. Pending jobs retain their original quote. */
export function simulate({name='no-event',events=[],market=marketCatalog(),credit=false,offers=1,duration=1,periods=30,costs=140000,city='synthetic_destination',cargo='synthetic_cargo',startingCash=1500000}={}){
 if(!Number.isSafeInteger(offers)||offers<0||offers>2||!Number.isSafeInteger(duration)||duration<1||duration>10||!Number.isSafeInteger(periods)||periods<1||periods>100)throw Error('bounds');
 safe(costs);safe(startingCash,10000000000);const w=world({events,market}),max=w.economy.maxBalance;
 let cash=startingCash,capital=0,loan=null,fees=0,profitTotal=0,paid=0,deliveries=0,rejected=0,acquiredAt=null,acquiredPeriod=null;
 if(credit){cash=checkedAdd(cash,2000000,max);fees=160000;loan={terms:{principal:2000000,fee:160000,liability:2160000,repaymentBps:3500,reserve:500000},remainingPrincipal:2000000,remainingFee:160000};}
 const pending=[],traces=[];
 const complete=(period)=>{
  for(let i=0;i<pending.length;){const job=pending[i];if(job.due>period){i++;continue;}pending.splice(i,1);
   const profit=job.q.finalGrossReward-costs,target=Number(BigInt(profit)*3500n/10000n),r=loan?repayment(loan,target,BigInt(cash)+BigInt(profit)):{total:0};
   cash=checkedAdd(cash,profit-r.total,max);paid+=r.total;profitTotal+=profit;deliveries++;
   if(loan){loan={...loan,remainingPrincipal:r.remainingPrincipal,remainingFee:r.remainingFee};if(r.debtAfter===0)loan=null;}
   if(acquiredAt===null&&cash>=5500000){cash-=5000000;capital=5000000;acquiredAt=deliveries;acquiredPeriod=period;}
   const debt=loan?loan.remainingPrincipal+loan.remainingFee:0;if(cash+capital-debt!==startingCash+profitTotal-fees)throw Error('conservation');
   traces.push({selectedAt:job.q.selectedAt,completedAt:period*60000,eventId:job.q.selectedEvent?.eventId??null,marketGross:job.q.marketGrossReward,finalGross:job.q.finalGrossReward,
    combinedBps:job.q.combinedAdjustmentBps,profit,payment:r.total,cash,debt});
  }
 };
 for(let period=0;period<periods;period++){
  complete(period);const free=(acquiredAt===null?1:2)-pending.length;
  for(let j=0;j<Math.min(offers,free);j++){const q=quote(w,period*60000,{city,cargo});if(q.finalGrossReward<=costs){rejected++;continue;}pending.push({q,due:period+duration});}
 }
 complete(periods+duration);
 return {name,deliveries,acquiredAt,acquiredPeriod,cash,capital,debt:loan?loan.remainingPrincipal+loan.remainingFee:0,fees,paid,profitTotal,rejected,traces};
}
export function scenarios(){return [
 {name:'no-event'}, {name:'positive',events:[event()]}, {name:'negative',events:[event({eventBps:-1000,type:'logistics_congestion'})]},
 {name:'begins-period5',events:[event({startsAt:300000})]}, {name:'expires-period5',events:[event({endsAt:300000})]},
 {name:'overlap-priority',events:[event({eventId:'low',priority:1}),event({eventId:'high',priority:2,eventBps:-500})]},
 {name:'equal-priority',events:[event({eventId:'z'}),event({eventId:'a',eventBps:500})]},
 {name:'city-matching',events:[event()]},{name:'cargo-matching',events:[event({cargoIds:['synthetic_secondary']})],cargo:'synthetic_secondary'},
 {name:'city-nonmatching',events:[event({cityIds:['synthetic_city']})]},
 {name:'event-high-demand',events:[event()],market:marketCatalog({demand:100,supply:0})},
 {name:'event-high-supply',events:[event()],market:marketCatalog({demand:0,supply:100})},
 {name:'maximum-cap',events:[event({eventBps:1500})],market:marketCatalog({demand:100,supply:0})},
 {name:'minimum-cap',events:[event({eventBps:-1000})],market:marketCatalog({demand:0,supply:100})},
 {name:'unprofitable',events:[event({eventBps:-1000})],costs:405000},
 {name:'credit-free-growth',events:[event()]},{name:'active-debt',events:[event()],credit:true},
 {name:'repayment-negative',events:[event({eventBps:-1000})],credit:true},
 {name:'expires-before-completion',events:[event({endsAt:60000})],duration:3},
 {name:'two-offers-short',events:[event()],offers:2},{name:'two-offers-long',events:[event()],offers:2,duration:3},
 {name:'zero-offers-debt',events:[event()],offers:0,credit:true},
 {name:'low-cash-tiny-profit',costs:449999,startingCash:0,credit:true},
 {name:'alternating-series',events:Array.from({length:6},(_,i)=>event({eventId:`period_${i}`,startsAt:i*300000,endsAt:(i+1)*300000,eventBps:i%2?-1000:1000})),market:marketCatalog({jitter:20})},
 ].map(simulate);}

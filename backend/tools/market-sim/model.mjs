import {marketFixtures} from '../../tests/market.fixtures.mjs';
import {validateMarketCatalog,marketQuote} from '../../functions/lib/marketModel.js';
import {configHash,checkedAdd,safe} from '../../functions/lib/economyModel.js';
import {repayment} from '../../functions/lib/bankModel.js';
// Standalone synthetic experiment. No SDK instance, credentials, database or network.
// Historical candidate amounts are compared, never edited or published as runtime config.
export function catalog({demand=50,supply=50,jitter=0,regional=false,cargoWeight=10000}={}){
 const f=marketFixtures('market_simulation');const economy={...f.economy,maxBalance:10000000000,startingBalance:1500000};
 const raw={...f.market,economyHash:configHash(economy),maxGrossReward:economy.maxBalance,jitter,
  regions:f.market.regions.map((r,i)=>({...r,demand:regional&&i===0?80:demand,supply:regional&&i===0?20:supply})),
  cargo:[{cargoId:'synthetic_cargo',weightBps:cargoWeight},{cargoId:'synthetic_secondary',weightBps:5000}]};
 return validateMarketCatalog(raw,raw.version,f.logistics,economy,['synthetic_cargo','synthetic_secondary']);
}
export function simulate({name='neutral',market=catalog(),costs=140000,credit=false,downturn=false,offersPerPeriod=1,chooseBest=false,
 cargoId='synthetic_cargo',startingCash=1500000,periods=30,maxBalance=10000000000}={}){
 safe(costs);safe(startingCash,maxBalance);if(!Number.isSafeInteger(periods)||periods<1||periods>100||!Number.isSafeInteger(offersPerPeriod)||offersPerPeriod<0||offersPerPeriod>2)throw Error('invalid bounds');
 let cash=startingCash,capital=0,loan=null,fees=0,principalReceived=0,principalPaid=0,feePaid=0,profitTotal=0,deliveries=0,acquiredAt=null,acquiredPeriod=null,rejected=0,blocked=null;
 const traces=[];
 if(credit){const principal=2000000,fee=160000;cash=checkedAdd(cash,principal,maxBalance);principalReceived=principal;fees=fee;
  loan={terms:{principal,fee,liability:principal+fee,repaymentBps:3500,reserve:500000},remainingPrincipal:principal,remainingFee:fee};}
 for(let epoch=0;epoch<periods;epoch++){
  const activeMarket=downturn&&epoch>=5?catalog({demand:0,supply:100}):market;
  const jobs=Math.min(offersPerPeriod,acquiredAt===null?1:2);
  for(let job=0;job<jobs;job++){
   const cities=chooseBest?activeMarket.regions.map(r=>r.cityId):['synthetic_destination'];
   const quotes=cities.map(city=>marketQuote(activeMarket,epoch,city,cargoId,450000)).sort((a,b)=>b.adjustedGrossReward-a.adjustedGrossReward||a.snapshot.cityId.localeCompare(b.snapshot.cityId));
   const quote=quotes[0],gross=quote.adjustedGrossReward;if(gross<=costs){rejected++;blocked='UNPROFITABLE_OFFER';continue;}
   const profit=gross-costs,target=Number(BigInt(profit)*3500n/10000n);
   const p=loan?repayment(loan,target,BigInt(cash)+BigInt(profit)):{total:0,fee:0,principal:0};
   const next=checkedAdd(cash,profit-p.total,maxBalance);cash=next;profitTotal+=profit;principalPaid+=p.principal;feePaid+=p.fee;
   if(loan){loan={...loan,remainingPrincipal:p.remainingPrincipal,remainingFee:p.remainingFee};if(p.debtAfter===0)loan=null;}
   deliveries++;if(acquiredAt===null&&cash>=5500000){cash-=5000000;capital=5000000;acquiredAt=deliveries;acquiredPeriod=epoch+1;}
   const debt=loan?loan.remainingPrincipal+loan.remainingFee:0;
   if(cash+capital-debt!==startingCash+profitTotal-fees)throw Error('conservation');
   traces.push({epoch,city:quote.snapshot.cityId,cargoId,adjustmentBps:quote.snapshot.adjustmentBps,gross,costs,profit,payment:p.total,cash,debt});
  }
 }
 return {name,deliveries,acquiredAt,acquiredPeriod,cash,capital,debt:loan?loan.remainingPrincipal+loan.remainingFee:0,fees,principalReceived,principalPaid,feePaid,profitTotal,rejected,blocked,offersPerPeriod,traces};
}
export function scenarios(){return [
 {name:'neutral-base'}, {name:'high-demand',market:catalog({demand:100,supply:0})}, {name:'high-supply',market:catalog({demand:0,supply:100})},
 {name:'low-profit-costs',costs:200000},{name:'high-profit-costs',costs:70000},
 {name:'regional-best',market:catalog({regional:true}),chooseBest:true},{name:'regional-fixed',market:catalog({regional:true})},
 {name:'cargo-half-weight',market:catalog({demand:80,supply:20}),cargoId:'synthetic_secondary'},
 {name:'epoch-jitter',market:catalog({jitter:20})}, {name:'credit-neutral',credit:true},{name:'credit-downturn',credit:true,downturn:true},
 {name:'credit-high-demand',credit:true,market:catalog({demand:100,supply:0})},
 {name:'two-offers-after-purchase',offersPerPeriod:2},{name:'credit-two-offers',credit:true,offersPerPeriod:2},
 {name:'unprofitable-with-debt',credit:true,costs:600000},{name:'zero-offer-supply',credit:true,offersPerPeriod:0},
 {name:'zero-starting-cash-net-completion',startingCash:0},{name:'tiny-profit-debt-reserve',startingCash:0,credit:true,costs:449999},
 ].map(simulate);}

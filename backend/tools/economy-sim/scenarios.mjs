import {money,basisPoints,add} from './model.mjs';
// Every value is an UNAPPROVED DESIGN CANDIDATE, never a runtime config.
export const baseCandidate = {
 version:'economy-00-v1',approval:'unapproved_design_candidate',name:'base-never',currency:'LGC',scale:2,
 startingCash:money('15000'),gross:money('4500'),costs:{fuel:money('700'),wages:money('400'),maintenance:money('200'),tolls:money('100')},
 truckCost:money('45000'),driverCost:money('5000'),purchaseReserve:money('5000'),cashReserve:money('5000'),deliveries:30,
 costTiming:'fuel_tolls_upfront',loan:{principal:money('20000'),chargeBps:800,unlockDeliveries:5,repaymentBps:3500},borrowAfter:null,autoAcquire:true,
};
export function candidate(overrides={}){return {...structuredClone(baseCandidate),...structuredClone(overrides)};}
export function scenarios(){
 const costs={low:{fuel:money('1000'),wages:money('600'),maintenance:money('300'),tolls:money('100')},base:baseCandidate.costs,
  high:{fuel:money('350'),wages:money('200'),maintenance:money('100'),tolls:money('50')}};
 return Object.entries(costs).flatMap(([name,costs])=>[candidate({name:`${name}-never`,costs}),candidate({name:`${name}-credit-5`,costs,borrowAfter:5})]);
}
export function sensitivities(){
 const increased=Object.fromEntries(Object.entries(baseCandidate.costs).map(([k,v])=>[k,add(v,basisPoints(v,2000))]));
 return [candidate(),candidate({name:'reward-minus-10',gross:baseCandidate.gross-basisPoints(baseCandidate.gross,1000)}),
  candidate({name:'costs-plus-20',costs:increased}),candidate({name:'combined-shock',gross:baseCandidate.gross-basisPoints(baseCandidate.gross,1000),costs:increased}),
  ...['fuel','wages','maintenance'].map(k=>candidate({name:`${k}-plus-50`,costs:{...baseCandidate.costs,[k]:add(baseCandidate.costs[k],basisPoints(baseCandidate.costs[k],5000))}})),
  candidate({name:'very-low-profit',gross:money('1500')}),
  candidate({name:'negative-profit',gross:money('1000')}),
  candidate({name:'zero-cash-upfront',startingCash:0}),candidate({name:'zero-cash-net-completion',startingCash:0,costTiming:'net_completion'}),
  candidate({name:'early-purchase-no-reserve',purchaseReserve:0}),
 ];
}

import {bankFixtures} from './bank.fixtures.mjs';
import {digest} from '../functions/lib/logisticsModel.js';
import {MARKET_POLICY,marketPolicyHash} from '../functions/lib/marketModel.js';
export function marketFixtures(version,{demand=50,supply=50,jitter=0,...options}={}){
 const f=bankFixtures(version,{gross:100,...options});
 const market={schemaVersion:1,version,approval:'synthetic_emulator_only',economyVersion:version,economyHash:f.config.economyHash,
  logisticsVersion:version,logisticsHash:f.config.logisticsHash,currency:f.economy.currency,scale:f.economy.scale,policy:MARKET_POLICY,policyHash:marketPolicyHash(),
  periodMs:60000,indexMax:100,jitter,seed:'synthetic_seed',sensitivityBps:25,minAdjustmentBps:-1000,maxAdjustmentBps:1500,maxGrossReward:f.economy.maxBalance,
  regions:f.logistics.cities.map(cityId=>({cityId,demand,supply,weightBps:10000})),cargo:f.config.cargo.map(c=>({cargoId:c.id,weightBps:10000}))};
 f.config={...f.config,schemaVersion:4,marketVersion:version,marketHash:digest(market)};f.rules.contractConfigHash=digest(f.config);
 return {...f,market};
}

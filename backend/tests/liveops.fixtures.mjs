import {marketFixtures} from './market.fixtures.mjs';
import {digest} from '../functions/lib/logisticsModel.js';
import {LIVEOPS_POLICY,liveOpsPolicyHash} from '../functions/lib/liveOpsModel.js';
export function syntheticEvent({eventId='synthetic_event',eventTime=0,eventBps=0,...patch}={}){
 return {eventId,type:'seasonal_trade_activity',titleKey:'events.synthetic.title',descriptionKey:'events.synthetic.description',startsAt:eventTime,endsAt:eventTime+30000,
  cityIds:['synthetic_destination'],cargoIds:['synthetic_cargo'],adjustmentBps:eventBps,priority:1,enabled:true,...patch};
}
export function liveOpsFixtures(version,{eventTime=0,eventBps=0,events,...options}={}){
 const f=marketFixtures(version,options),liveOps={schemaVersion:1,version,approval:'synthetic_emulator_only',economyVersion:version,economyHash:f.config.economyHash,
  logisticsVersion:version,logisticsHash:f.config.logisticsHash,marketVersion:version,marketHash:digest(f.market),policy:LIVEOPS_POLICY,policyHash:liveOpsPolicyHash(),currency:f.economy.currency,scale:f.economy.scale,
  minEventBps:-1000,maxEventBps:1500,minCombinedBps:-1000,maxCombinedBps:1500,maxEvents:16,events:events??[syntheticEvent({eventTime,eventBps})]};
 f.config={...f.config,schemaVersion:5,liveOpsVersion:version,liveOpsHash:digest(liveOps)};f.rules.contractConfigHash=digest(f.config);return {...f,liveOps};
}

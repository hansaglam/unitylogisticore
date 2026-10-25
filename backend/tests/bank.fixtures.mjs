import {settlementFixtures} from './settlement.fixtures.mjs';
import {bankPolicy} from '../functions/lib/bankModel.js';
import {digest} from '../functions/lib/logisticsModel.js';
export function bankFixtures(version,{legacy=3,...options}={}){
 const f=settlementFixtures(version,{gross:24,...options});
 if(legacy===1){f.config.schemaVersion=1;delete f.config.costCatalogVersion;delete f.config.costCatalogHash;}
 if(legacy===3){f.config.schemaVersion=3;f.config.bankPolicy=bankPolicy();}
 f.rules.contractConfigHash=digest(f.config);
 const bank={schemaVersion:1,version,approval:'synthetic_emulator_only',economyVersion:version,economyHash:f.config.economyHash,
  operatingPolicy:'synthetic_net_claim_v1',bankPolicyHash:bankPolicy().policyHash,currency:f.economy.currency,scale:f.economy.scale,
  minLevel:1,maxLevel:3,tiers:[{from:1,to:1,maximum:40},{from:2,to:2,maximum:60},{from:3,to:3,maximum:80}],
  minPrincipal:10,increment:10,chargeBps:800,minFee:1,repaymentBps:3500,reserve:5,quoteTtlMs:60000,budgetWindowMs:60000,maxQuoteRequests:20,maxQuoteWrites:10,maxLiability:10000};
 return {...f,bank};
}

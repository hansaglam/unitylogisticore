import {contractFixtures} from './contracts.fixtures.mjs';
import {digest} from '../functions/lib/logisticsModel.js';
export function settlementFixtures(version,{costs={fuelCost:1,driverCost:1,maintenanceCost:1,tollCost:1},gross=6}={}){
  const f=contractFixtures(version);
  f.config.templates=f.config.templates.map(t=>({...t,quantity:1,rewardPerUnit:gross}));
  const costCatalog={schemaVersion:1,version,approval:'synthetic_emulator_only',economyVersion:version,economyHash:f.config.economyHash,
    logisticsVersion:version,logisticsHash:f.config.logisticsHash,policy:'synthetic_net_claim_v1',currency:f.economy.currency,scale:f.economy.scale,maxCost:f.economy.maxBalance,
    entries:f.config.templates.map(t=>({templateId:t.id,routeId:t.routeId,cargoId:t.cargoId,costs:{...costs}}))};
  f.config={...f.config,schemaVersion:2,costCatalogVersion:version,costCatalogHash:digest(costCatalog)};
  const rules={schemaVersion:1,version,approval:'synthetic_emulator_only',contractConfigHash:digest(f.config),logisticsHash:f.config.logisticsHash,economyHash:f.config.economyHash,
    routes:[{routeId:'synthetic_route',cargoId:'synthetic_cargo',durationMs:1,xp:10}]};
  return {...f,costCatalog,rules};
}

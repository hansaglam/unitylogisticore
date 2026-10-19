import { logisticsFixture, economyFixture } from './logistics.fixtures.mjs';
import { digest } from '../functions/lib/logisticsModel.js';
import { configHash, validateConfig } from '../functions/lib/economyModel.js';
export function contractFixtures(version) {
  const economy=economyFixture(version);
  const logistics=logisticsFixture(version);logistics.schemaVersion=2;logistics.cities.push('synthetic_destination');
  logistics.assets=logistics.assets.map(a=>({...a,capabilities:a.kind==='driver'?['synthetic_license']:['synthetic_cargo','synthetic_vehicle'],capacity:a.kind==='driver'?null:{unit:'synthetic_units',amount:10}}));
  const config={schemaVersion:1,version,approval:'synthetic_emulator_only',logisticsVersion:version,logisticsHash:digest(logistics),
    economyVersion:version,economyHash:configHash(validateConfig(economy,version)),epochDurationMs:600000,offerTtlMs:600000,maxOffers:3,maxAcceptedPerEpoch:2,
    routes:[{id:'synthetic_route',originCityId:'synthetic_city',destinationCityId:'synthetic_destination',distance:1,unit:'synthetic_distance',verification:'synthetic_fixture'}],
    cargo:[{id:'synthetic_cargo',unit:'synthetic_units',capability:'synthetic_cargo'}],
    templates:Array.from({length:3},(_,i)=>({id:`synthetic_job_${i}`,routeId:'synthetic_route',cargoId:'synthetic_cargo',quantity:2,rewardPerUnit:3,minLevel:1,truckCapability:'synthetic_vehicle',driverCapability:'synthetic_license',trailerCapability:null}))};
  return {economy,logistics,config};
}
export const candidates=[{assetId:'starter_truck',kind:'truck'},{assetId:'starter_driver',kind:'driver'}];

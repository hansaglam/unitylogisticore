// Every amount, capability, city, duration and level here is synthetic emulator data, not product defaults.
import { contractFixtures } from './contracts.fixtures.mjs';
import { digest } from '../functions/lib/logisticsModel.js';
export function commerceFixtures(version) {
 const f=contractFixtures(version); f.economy.startingBalance=1000;
 f.logistics.warehouses.push({...f.logistics.warehouses[0],id:'test_warehouse_2',level:2,capacityUnits:20},
  {...f.logistics.warehouses[0],id:'test_warehouse_3',level:3,capacityUnits:30});
 f.config.logisticsHash=digest(f.logistics); f.config.economyHash=digest(f.economy);
 const commerce={schemaVersion:1,version,approval:'synthetic_emulator_only',economyVersion:version,economyHash:digest(f.economy),
  rulesVersion:f.economy.rulesVersion,logisticsVersion:version,logisticsHash:digest(f.logistics),items:[
   {id:'truck',kind:'buyVehicle',catalogId:'test_truck',price:20,requiredLevel:1,expectedLevel:0},
   {id:'driver',kind:'hireDriver',catalogId:'test_driver',price:10,requiredLevel:1,expectedLevel:0},
   {id:'trailer',kind:'acquireTrailer',catalogId:'test_trailer',price:10,requiredLevel:1,expectedLevel:0},
   {id:'open',kind:'openWarehouse',catalogId:'test_warehouse',price:10,requiredLevel:1,expectedLevel:0},
   {id:'upgrade',kind:'upgradeWarehouse',catalogId:'test_warehouse_2',price:10,requiredLevel:1,expectedLevel:1},
   {id:'upgrade3',kind:'upgradeWarehouse',catalogId:'test_warehouse_3',price:10,requiredLevel:1,expectedLevel:2},
   {id:'locked',kind:'buyVehicle',catalogId:'test_truck',price:20,requiredLevel:3,expectedLevel:0}]};
 return {...f,commerce};
}

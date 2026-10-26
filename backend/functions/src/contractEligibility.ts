import { validateMarketCatalog } from "./marketModel";
import { Firestore, Transaction } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { versionId } from "./economyModel";
import { readStarter } from "./fleet";
import { CompanyState, catalogReader } from "./logisticsContext";
import { digest, validateAsset } from "./logisticsModel";
import { ContractTerms, validateContractConfig } from "./contractModel";
import { validateCostCatalog } from "./operatingCosts";

export async function readContractConfig(tx: Transaction, db: Firestore, state: CompanyState, version: string) {
  if (!state.economy) throw new CommandError("CONTRACT_INELIGIBLE");
  const raw = (await tx.get(db.collection("v2ContractCatalog").doc(versionId(version)))).data();
  const logistics = await catalogReader(tx,db)(raw?.logisticsVersion);
  const config=validateContractConfig(raw,version,logistics,state.economy.config);
  const costs=config.schemaVersion>=2?validateCostCatalog((await tx.get(db.collection("v2OperatingCostCatalog").doc(config.costCatalogVersion!))).data(),config.costCatalogVersion!,logistics,state.economy.config):undefined;
  const market=config.schemaVersion===4?validateMarketCatalog((await tx.get(db.collection("v2MarketCatalog").doc(config.marketVersion!))).data(),config.marketVersion!,logistics,state.economy.config,config.cargo.map(c=>c.id)):undefined;
  if(market && config.marketHash!==digest(market))throw new CommandError("FAILED_PRECONDITION");
  return { config, logistics, costs, market };
}
/** Point reads only, server-selected candidate set. Eligibility is not a reservation promise. */
export async function requireContractEligibility(tx: Transaction, db: Firestore, state: CompanyState, terms: ContractTerms) {
  const getCatalog = catalogReader(tx,db);
  if (!state.economy || state.economy.level < terms.minLevel || !(await readStarter(tx,db,state,getCatalog)).ready) throw new CommandError("CONTRACT_INELIGIBLE");
  const logistics = await getCatalog(terms.logisticsVersion);
  const records = await tx.getAll(...terms.candidateAssets.map(a => state.companyRef.collection("assets").doc(a.assetId)));
  for (let i=0;i<terms.candidateAssets.length;i++) {
    const candidate = terms.candidateAssets[i]!;
    const asset = validateAsset(records[i]!.data(),candidate.assetId,state.companyId,logistics);
    const definition = logistics.assets.find(a => a.id === asset.catalogId)!;
    const requirement = terms.requirements[asset.kind];
    if (logistics.schemaVersion !== 2 || asset.kind !== candidate.kind || !asset.available || !definition.capabilities ||
        (requirement !== null && !definition.capabilities.includes(requirement))) throw new CommandError("CONTRACT_INELIGIBLE");
    const carriesCargo = asset.kind === (terms.requirements.trailer === null ? "truck" : "trailer");
    if (carriesCargo && (!definition.capabilities.includes(terms.cargo.capability) || !definition.capacity ||
        definition.capacity.unit !== terms.cargo.unit || definition.capacity.amount < terms.quantity)) throw new CommandError("CONTRACT_INELIGIBLE");
  }
  if (terms.requirements.trailer !== null && !terms.candidateAssets.some(a => a.kind === "trailer")) throw new CommandError("CONTRACT_INELIGIBLE");
}

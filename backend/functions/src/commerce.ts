import { FieldPath, FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { CommandError, executeCommand } from "./commands";
import { readFoundation } from "./company";
import { clockTime, ServerClock, serverClock } from "./contractModel";
import { operationId, planEconomyMutation } from "./economy";
import { exact, invalid, safe, versionId } from "./economyModel";
import { readStarter } from "./fleet";
import { requireUid } from "./identity";
import { catalogReader, CompanyState } from "./logisticsContext";
import { digest, pageRequest, validateAsset, validateWarehouse } from "./logisticsModel";
import { assertLocalEnvironment } from "./localEnvironment";
import { acquisitionPayload, CommerceKind, validateAcquisitionOffer, validateCommerceCatalog } from "./commerceModel";

/** All objects and caches belong to this transaction attempt. No publisher or client-selected config. */
function offerReader(tx: Transaction, db: Firestore, state: CompanyState) {
  const getLogistics = catalogReader(tx, db);
  const catalogs = new Map<string, { commerce: ReturnType<typeof validateCommerceCatalog>; logistics: Awaited<ReturnType<typeof getLogistics>> }>();
  return async (id: string) => {
    if (!state.economy) invalid();
    const ref = state.companyRef.collection("acquisitionOffers").doc(versionId(id));
    const raw = (await tx.get(ref)).data(), version = versionId(raw?.catalogVersion);
    if (!catalogs.has(version)) {
      const config = (await tx.get(db.collection("v2CommerceCatalog").doc(version))).data();
      const logistics = await getLogistics(config?.logisticsVersion);
      catalogs.set(version, { commerce: validateCommerceCatalog(config,version,logistics,state.economy.config), logistics });
    }
    const { commerce, logistics } = catalogs.get(version)!;
    const offer = validateAcquisitionOffer(raw,id,state.companyId,commerce,logistics,state.economy.config);
    const warehouse = offer.item.kind === "openWarehouse" || offer.item.kind === "upgradeWarehouse";
    const targetRef = state.companyRef.collection(warehouse ? "warehouses" : "assets").doc(offer.targetId);
    const target = (await tx.get(targetRef)).data();
    const opId = operationId(state.companyId,offer.sourceId);
    const claimRef = state.companyRef.collection("claims").doc(offer.sourceId), ledgerRef = state.companyRef.collection("ledger").doc(opId);
    const [claim, ledger] = await tx.getAll(claimRef,ledgerRef);
    let currentWarehouse: ReturnType<typeof validateWarehouse> | undefined;
    if (target) {
      const currentCatalog = await getLogistics(target.catalogVersion);
      if (warehouse) currentWarehouse = validateWarehouse(target,offer.targetId,state.companyId,currentCatalog);
      else {
        const asset = validateAsset(target,offer.targetId,state.companyId,currentCatalog);
        if (asset.catalogId !== offer.item.catalogId || asset.catalogVersion !== logistics.version) invalid();
      }
    }
    if (offer.status === "consumed") {
      if (!target || offer.operationId !== opId) invalid();
      // Historical warehouse receipts remain valid after legitimate later upgrades.
      const definition = logistics.warehouses.find(w => w.id === offer.item.catalogId);
      if (warehouse && (!currentWarehouse || currentWarehouse.level < definition!.level ||
          currentWarehouse.capacityModelId !== definition!.capacityModelId)) invalid();
      const c = claim!.data(), l = ledger!.data();
      exact(c,"schemaVersion,source,operationId,configVersion,committedAt");
      exact(l,"schemaVersion,operationId,reason,delta,balanceAfter,currency,scale,source,configVersion,rulesVersion,committedAt");
      if (c.schemaVersion !== 1 || c.source !== offer.sourceId || c.operationId !== opId || c.configVersion !== commerce.economyVersion || !(c.committedAt instanceof Timestamp) ||
          l.schemaVersion !== 1 || l.operationId !== opId || l.source !== offer.sourceId || l.reason !== "commerce_purchase" || l.delta !== -offer.item.price ||
          l.currency !== state.economy.config.currency || l.scale !== state.economy.config.scale || l.configVersion !== commerce.economyVersion ||
          l.rulesVersion !== commerce.rulesVersion || !(l.committedAt instanceof Timestamp)) invalid();
      safe(l.balanceAfter,state.economy.config.maxBalance);
    } else if (claim!.exists || ledger!.exists) invalid();
    return { ref, offer, commerce, logistics, targetRef, target, currentWarehouse, opId };
  };
}
type LoadedOffer = Awaited<ReturnType<ReturnType<typeof offerReader>>>;
/** Eligibility is display information; mutation rechecks it in its own transaction. Corrupt records throw. */
function eligibility(loaded: LoadedOffer, state: CompanyState, starterReady: boolean, now: number): string | null {
  const { offer, target, currentWarehouse, logistics } = loaded;
  if (offer.status !== "available") return "OFFER_CONSUMED";
  if (now < offer.createdAt.toMillis()) return "OFFER_NOT_ACTIVE";
  if (now >= offer.expiresAt.toMillis()) return "OFFER_EXPIRED";
  if (!starterReady || state.economy!.level < offer.item.requiredLevel) return "ACQUISITION_INELIGIBLE";
  if (offer.item.kind === "upgradeWarehouse") {
    const next = logistics.warehouses.find(w => w.id === offer.item.catalogId)!;
    if (!currentWarehouse || currentWarehouse.status !== "operational" || currentWarehouse.level !== offer.item.expectedLevel ||
        currentWarehouse.capacityModelId !== next.capacityModelId || Number(currentWarehouse.capacityUnits) > next.capacityUnits ||
        Number(currentWarehouse.usedCapacity) > next.capacityUnits) return "WAREHOUSE_UPGRADE_INVALID";
  } else if (target) return "ACQUISITION_TARGET_EXISTS";
  if (state.economy!.balance < offer.item.price) return "INSUFFICIENT_FUNDS";
  return null;
}
export function acquisitionCommand(db: Firestore, auth: {uid:string} | undefined, data: unknown, kind: CommerceKind, clock: ServerClock = serverClock) {
  const attempts = new WeakMap<Transaction,{state:CompanyState;loaded:LoadedOffer;starterReady:boolean}>();
  return executeCommand(db,auth,data,{
    name:kind,validate:acquisitionPayload,
    async resolveCompany(tx,uid,payload) {
      const state = await readFoundation(tx,db,uid); if (!state.economy) invalid();
      const starter = await readStarter(tx,db,state);
      const loaded = await offerReader(tx,db,state)(payload.offerId);
      if (loaded.offer.item.kind !== kind) invalid();
      attempts.set(tx,{state,loaded,starterReady:starter.ready}); return state.companyId;
    },
    async apply(tx) {
      const {state,loaded,starterReady} = attempts.get(tx)!;
      const now = clockTime(clock), reason = eligibility(loaded,state,starterReady,now);
      if (reason) throw new CommandError(reason as "OFFER_EXPIRED" | "OFFER_CONSUMED" | "OFFER_NOT_ACTIVE" | "ACQUISITION_INELIGIBLE" | "WAREHOUSE_UPGRADE_INVALID" | "ACQUISITION_TARGET_EXISTS" | "INSUFFICIENT_FUNDS");
      const {offer,logistics,targetRef} = loaded;
      const spending = planEconomyMutation(tx,state,state.economy!.config,offer.sourceId,-offer.item.price,0,"commerce_purchase");
      const timestamp = FieldValue.serverTimestamp();
      // Write phase: no further Firestore reads, nested transactions or external effects.
      if (offer.item.kind === "openWarehouse" || offer.item.kind === "upgradeWarehouse") {
        const definition = logistics.warehouses.find(w => w.id === offer.item.catalogId)!;
        const fields = {catalogId:definition.id,catalogVersion:logistics.version,catalogHash:digest(logistics),level:definition.level,capacityModelId:definition.capacityModelId};
        if (offer.item.kind === "upgradeWarehouse") tx.update(targetRef,fields);
        else tx.create(targetRef,{schemaVersion:1,warehouseId:offer.targetId,companyId:state.companyId,...fields,cityId:definition.cityId,type:definition.type,
          status:"operational",usedCapacity:0,acquiredAt:timestamp});
      } else {
        const definition = logistics.assets.find(a => a.id === offer.item.catalogId)!;
        tx.create(targetRef,{schemaVersion:1,assetId:offer.targetId,companyId:state.companyId,kind:definition.kind,catalogId:definition.id,
          catalogVersion:logistics.version,catalogHash:digest(logistics),status:"operational",assignmentId:null,acquiredAt:timestamp});
      }
      const paid = spending.apply();
      tx.update(loaded.ref,{status:"consumed",consumedAt:Timestamp.fromMillis(now),operationId:paid.operationId});
      return {offerId:offer.offerId,kind,targetId:offer.targetId,status:"consumed",price:String(offer.item.price),currency:paid.currency,scale:paid.scale,
        balance:paid.balance,operationId:paid.operationId};
    }
  });
}
export async function readMyAcquisitionOffers(db: Firestore, auth: {uid:string} | undefined, data: unknown, clock: ServerClock = serverClock) {
  assertLocalEnvironment(process.env,true); const uid = requireUid(auth), page = pageRequest(data);
  try {
    const result = await db.runTransaction(async tx => {
      const state = await readFoundation(tx,db,uid); if (!state.economy) invalid();
      const starter = await readStarter(tx,db,state), readOffer = offerReader(tx,db,state);
      let query = state.companyRef.collection("acquisitionOffers").orderBy(FieldPath.documentId()).limit(page.limit+1);
      if (page.after) query = query.startAfter(page.after);
      const records = await tx.get(query), loaded:LoadedOffer[] = [];
      for (const doc of records.docs.slice(0,page.limit)) loaded.push(await readOffer(doc.id));
      const now = clockTime(clock);
      return {protocolVersion:1,serverTime:new Date(now).toISOString(),result:{items:loaded.map(entry => {
        const {offer,commerce,logistics} = entry, definition = logistics.warehouses.find(w => w.id === offer.item.catalogId);
        const reason = eligibility(entry,state,starter.ready,now);
        return {offerId:offer.offerId,kind:offer.item.kind,itemId:offer.item.id,catalogId:offer.item.catalogId,targetId:offer.targetId,
          price:String(offer.item.price),currency:state.economy!.config.currency,scale:state.economy!.config.scale,requiredLevel:offer.item.requiredLevel,
          expectedLevel:offer.item.expectedLevel,cityId:definition?.cityId??null,nextLevel:definition?.level??null,
          catalogVersion:commerce.version,logisticsVersion:logistics.version,rulesVersion:commerce.rulesVersion,
          createdAt:offer.createdAt.toDate().toISOString(),expiresAt:offer.expiresAt.toDate().toISOString(),status:offer.status,
          expired:now>=offer.expiresAt.toMillis(),eligible:reason===null,ineligibilityCode:reason};
      }),nextCursor:records.size>page.limit?records.docs[page.limit-1]!.id:null}};
    },{readOnly:true});
    return result;
  } catch(error) { if (error instanceof CommandError) throw error; throw new CommandError("INTERNAL"); }
}

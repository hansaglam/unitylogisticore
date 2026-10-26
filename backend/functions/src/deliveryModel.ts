import { DocumentData, Timestamp } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { ContractTerms } from "./contractModel";
import { exact, invalid, safe, versionId } from "./economyModel";
import { assignmentId, Selection, validateSelection } from "./fleet";
import { digest } from "./logisticsModel";

export const MAX_TIMESTAMP = 253402300799999;
export function arrivalTime(start: number, duration: number) {
  safe(start, MAX_TIMESTAMP); if (safe(duration, 604800000) < 1 || duration > MAX_TIMESTAMP - start) invalid();
  return start + duration;
}
export function validateDeliveryRules(raw: DocumentData | undefined, terms: ContractTerms) {
  exact(raw, "schemaVersion,version,approval,contractConfigHash,logisticsHash,economyHash,routes");
  if (raw.schemaVersion !== 1 || raw.version !== terms.configVersion || raw.approval !== "synthetic_emulator_only" ||
      raw.contractConfigHash !== terms.configHash || raw.logisticsHash !== terms.logisticsHash || raw.economyHash !== terms.economyHash ||
      !Array.isArray(raw.routes) || raw.routes.length < 1 || raw.routes.length > 5) invalid();
  const routes = (raw.routes as DocumentData[]).map(r => {
    exact(r, "routeId,cargoId,durationMs,xp"); arrivalTime(0,r.durationMs); safe(r.xp);
    return { routeId:versionId(r.routeId), cargoId:versionId(r.cargoId), durationMs:r.durationMs as number, xp:r.xp as number };
  });
  if (new Set(routes.map(r => `${r.routeId}:${r.cargoId}`)).size !== routes.length) invalid();
  const rule = routes.find(r => r.routeId === terms.route.id && r.cargoId === terms.cargo.id);if (!rule) invalid();
  return { rules: {schemaVersion:1,version:raw.version as string,approval:"synthetic_emulator_only",contractConfigHash:terms.configHash,
    logisticsHash:terms.logisticsHash,economyHash:terms.economyHash,routes}, rule };
}
export function deliveryIdFor(companyId:string, contractId:string) { return digest({companyId,contractId:versionId(contractId),domain:"delivery_v1"}); }
export function startPayload(value:unknown) {
  try {
    if (!value || typeof value !== "object" || Array.isArray(value)) invalid();
    const raw=value as DocumentData;
    exact(raw,"contractId,truckId,driverId"+(Object.hasOwn(raw,"trailerId")?",trailerId":""));
    const result={contractId:versionId(raw.contractId),truckId:versionId(raw.truckId),driverId:versionId(raw.driverId)};
    return Object.hasOwn(raw,"trailerId")?{...result,trailerId:versionId(raw.trailerId)}:result;
  } catch {throw new CommandError("INVALID_ARGUMENT");}
}
export function startSelection(payload:ReturnType<typeof startPayload>):Selection {
  return validateSelection([{assetId:payload.truckId,kind:"truck"},{assetId:payload.driverId,kind:"driver"},
    ...("trailerId" in payload ? [{assetId:payload.trailerId,kind:"trailer" as const}]:[])]);
}
export function claimPayload(value:unknown) {
  try {exact(value as DocumentData,"deliveryId");return {deliveryId:versionId((value as DocumentData).deliveryId)};}
  catch {throw new CommandError("INVALID_ARGUMENT");}
}
export function validateDelivery(raw:DocumentData|undefined,id:string,companyId:string,terms:ContractTerms) {
  if(!raw)invalid();
  exact(raw,"schemaVersion,deliveryId,companyId,contractId,status,assignmentId,assets,termsHash,routeId,cargoId,startedAt,expectedArrivalAt,deliveryRulesVersion,deliveryRulesHash,rewardState"+
    (raw.status==="completed"?",completedAt":"")+(terms.settlement?",settlement":""));
  if(raw.schemaVersion!==(terms.market?4:terms.bankPolicy?3:terms.settlement?2:1) || (terms.settlement && digest(raw.settlement)!==digest(terms.settlement)) || raw.companyId!==companyId || raw.deliveryId!==id || id!==deliveryIdFor(companyId,raw.contractId) ||
    raw.assignmentId!==assignmentId(companyId,id) || !["active","completed"].includes(raw.status) ||
    raw.termsHash!==digest(terms) || raw.routeId!==terms.route.id || raw.cargoId!==terms.cargo.id ||
    raw.deliveryRulesVersion!==terms.configVersion || typeof raw.deliveryRulesHash!=="string" || !/^[a-f0-9]{64}$/.test(raw.deliveryRulesHash) ||
    digest(validateSelection(raw.assets))!==digest(terms.candidateAssets) || !(raw.startedAt instanceof Timestamp) || !(raw.expectedArrivalAt instanceof Timestamp) ||
    raw.expectedArrivalAt.toMillis()<=raw.startedAt.toMillis() ||
    raw.rewardState!==(raw.status==="active"?"unclaimed":"claimed")) invalid();
  arrivalTime(raw.startedAt.toMillis(),raw.expectedArrivalAt.toMillis()-raw.startedAt.toMillis());
  if(raw.status==="completed" && (!(raw.completedAt instanceof Timestamp) || raw.completedAt.toMillis()<raw.expectedArrivalAt.toMillis()))invalid();
  return {deliveryId:id,contractId:raw.contractId as string,status:raw.status as "active"|"completed",assignmentId:raw.assignmentId as string,
    assets:raw.assets as Selection,startedAt:raw.startedAt as Timestamp,expectedArrivalAt:raw.expectedArrivalAt as Timestamp,
    deliveryRulesHash:raw.deliveryRulesHash as string,completedAt:raw.status==="completed"?raw.completedAt as Timestamp:null};
}

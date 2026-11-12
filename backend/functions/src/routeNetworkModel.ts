import { DocumentData } from "firebase-admin/firestore";
import { exact, invalid, safe, versionId } from "./economyModel";
import { digest } from "./logisticsModel";
import type { ContractConfig } from "./contractModel";

export const ROUTE_NETWORK_COLLECTION = "v2RouteNetworkCatalog";
export const DISTANCE_PROVENANCE_LEGACY_APPROXIMATE = "legacy_approximate";
export const DISTANCE_PROVENANCE_KGM_IL_MERKEZ_2026_03_03 = "kgm_il_merkez_2026_03_03";
export const ROUTE_VERIFICATION_SYNTHETIC_FIXTURE = "synthetic_fixture";
export const ROUTE_VERIFICATION_KGM_ILMESAFE = "kgm_ilmesafe";

const PROVENANCE = [DISTANCE_PROVENANCE_LEGACY_APPROXIMATE, DISTANCE_PROVENANCE_KGM_IL_MERKEZ_2026_03_03] as const;
const VERIFICATION = [ROUTE_VERIFICATION_SYNTHETIC_FIXTURE, ROUTE_VERIFICATION_KGM_ILMESAFE] as const;
type DistanceProvenance = typeof PROVENANCE[number];
type RouteVerification = typeof VERIFICATION[number];

function provenanceOf(value: unknown): DistanceProvenance {
  if (value === DISTANCE_PROVENANCE_LEGACY_APPROXIMATE || value === DISTANCE_PROVENANCE_KGM_IL_MERKEZ_2026_03_03) return value;
  invalid();
}
function verificationOf(value: unknown): RouteVerification {
  if (value === ROUTE_VERIFICATION_SYNTHETIC_FIXTURE || value === ROUTE_VERIFICATION_KGM_ILMESAFE) return value;
  invalid();
}

type RouteEdge = {
  id: string;
  originCityId: string;
  destinationCityId: string;
  distance: number;
  unit: string;
  verification: RouteVerification;
  difficultyMilli: number;
  tollCost: number;
};

function edge(data: DocumentData, cityIds: string[], unit: string, expected: RouteVerification): RouteEdge {
  exact(data, "id,originCityId,destinationCityId,distance,unit,verification,difficultyMilli,tollCost");
  const originCityId = versionId(data.originCityId);
  const destinationCityId = versionId(data.destinationCityId);
  const id = versionId(data.id);
  const verification = verificationOf(data.verification);
  if (id !== `${originCityId}-${destinationCityId}` || originCityId === destinationCityId ||
      !cityIds.includes(originCityId) || !cityIds.includes(destinationCityId) ||
      verification !== expected || data.unit !== unit || safe(data.distance) < 1 ||
      safe(data.difficultyMilli, 100) < 1 || safe(data.tollCost) < 1) invalid();
  return {
    id, originCityId, destinationCityId, distance: data.distance as number, unit,
    verification, difficultyMilli: data.difficultyMilli as number, tollCost: data.tollCost as number,
  };
}

function verificationFor(provenance: DistanceProvenance): RouteVerification {
  return provenance === DISTANCE_PROVENANCE_KGM_IL_MERKEZ_2026_03_03
    ? ROUTE_VERIFICATION_KGM_ILMESAFE : ROUTE_VERIFICATION_SYNTHETIC_FIXTURE;
}

/** Immutable world graph. Not an offer pool. */
export function validateRouteNetwork(data: DocumentData | undefined, version: string) {
  exact(data, "schemaVersion,version,approval,cityIds,distanceUnit,distanceProvenance,routes");
  const distanceProvenance = provenanceOf(data.distanceProvenance);
  if (data.schemaVersion !== 1 || data.version !== versionId(version) || data.approval !== "synthetic_emulator_only") invalid();
  const distanceUnit = versionId(data.distanceUnit);
  if (!Array.isArray(data.cityIds) || data.cityIds.length !== 8 || !Array.isArray(data.routes) || data.routes.length !== 56) invalid();
  const cityIds = (data.cityIds as unknown[]).map(versionId);
  if (new Set(cityIds).size !== 8) invalid();
  const expectedVerification = verificationFor(distanceProvenance);
  const routes = (data.routes as DocumentData[]).map(row => edge(row, cityIds, distanceUnit, expectedVerification));
  if (new Set(routes.map(r => r.id)).size !== 56) invalid();
  const undirected = new Set(routes.map(r => [r.originCityId, r.destinationCityId].sort().join("|")));
  if (undirected.size !== 28) invalid();
  for (const pair of undirected) {
    const [a, b] = pair.split("|") as [string, string];
    const forward = routes.find(r => r.originCityId === a && r.destinationCityId === b);
    const reverse = routes.find(r => r.originCityId === b && r.destinationCityId === a);
    if (!forward || !reverse) invalid();
    if (distanceProvenance === DISTANCE_PROVENANCE_KGM_IL_MERKEZ_2026_03_03 && forward.distance !== reverse.distance) invalid();
  }
  return {
    schemaVersion: 1 as const, version, approval: "synthetic_emulator_only" as const, cityIds, distanceUnit,
    distanceProvenance, routes,
  };
}
export type RouteNetwork = ReturnType<typeof validateRouteNetwork>;

export function assertContractRoutesOnNetwork(config: ContractConfig, network: RouteNetwork) {
  if (config.routes.length < 1 || config.routes.length > 5) invalid();
  for (const route of config.routes) {
    const authoritative = network.routes.find(r => r.id === route.id);
    if (!authoritative || authoritative.originCityId !== route.originCityId ||
        authoritative.destinationCityId !== route.destinationCityId ||
        authoritative.distance !== route.distance || authoritative.unit !== route.unit ||
        route.verification !== authoritative.verification) invalid();
  }
}

export function requireRouteNetwork(data: DocumentData | undefined, version: string, expectedHash: string) {
  const network = validateRouteNetwork(data, version);
  if (digest(network) !== expectedHash) invalid();
  return network;
}

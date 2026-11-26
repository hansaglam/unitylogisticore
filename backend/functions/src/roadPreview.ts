import { Firestore } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { readFoundation } from "./company";
import { validateHeadquartersRecord } from "./headquartersRecord";
import { validateInitialHeadquarters } from "./headquartersCatalog";
import { requireUid } from "./identity";
import { assertLocalEnvironment } from "./localEnvironment";
import { fetchLocalRoadPreview } from "./localRoadRouting";

export type RoadPreviewRequest = {
  origin: { countryCode: string; cityKey: string };
  destination: { countryCode: string; cityKey: string };
};

export function validateRoadPreviewRequest(data: unknown): RoadPreviewRequest {
  if (!data || typeof data !== "object" || Array.isArray(data) ||
      Object.keys(data).sort().join(",") !== "destination,origin,protocolVersion" ||
      (data as Record<string, unknown>).protocolVersion !== 1)
    throw new CommandError("INVALID_ARGUMENT");
  const req = data as Record<string, unknown>;
  const origin = validateInitialHeadquarters(req.origin);
  const destination = validateInitialHeadquarters(req.destination);
  if (origin.countryCode === destination.countryCode &&
      origin.cityKey === destination.cityKey)
    throw new CommandError("INVALID_ARGUMENT");
  return { origin, destination };
}

const CACHE_TTL_MS = 120000;
const MAX_CACHE = 64;
const cache = new Map<string, { until: number; result: Awaited<ReturnType<typeof fetchLocalRoadPreview>> }>();

/**
 * Read-only route geometry *preview* for a signed-in company. The origin MUST
 * be its authoritative HQ; destination must be a server-approved city. It
 * does not grant warehouse operation, shipping, border access, vehicle rights,
 * contracts, rewards, time billing or travel start.
 *
 * This is local emulator only. Connection to OSRM happens after Firestore
 * account reads, never inside a Firestore transaction.
 */
export async function readMyRoadRoutePreview(db: Firestore, auth: { uid: string } | undefined,
  data: unknown, routeFetch: typeof fetchLocalRoadPreview = fetchLocalRoadPreview) {
  assertLocalEnvironment(process.env, true);
  const uid = requireUid(auth);
  const request = validateRoadPreviewRequest(data);

  await db.runTransaction(async tx => {
    const foundation = await readFoundation(tx, db, uid);
    if (!foundation.companyData)
      throw new CommandError("FAILED_PRECONDITION");
    const headquarters = await tx.get(foundation.companyRef.collection("state").doc("headquarters"));
    if (!headquarters.exists)
      throw new CommandError("FAILED_PRECONDITION"); // legacy account: no guessed city
    const verified = validateHeadquartersRecord(headquarters.data(), uid, foundation.companyId);
    if (request.origin.countryCode !== verified.countryCode ||
        request.origin.cityKey !== verified.cityKey)
      throw new CommandError("PERMISSION_DENIED");
  }, { readOnly: true });

  // Coordinates come exclusively from the trusted pinned 248-city catalog.
  // Preview cache stores only public geometry and never monetary account state.
  const key = [request.origin.countryCode, request.origin.cityKey,
    request.destination.countryCode, request.destination.cityKey].join("|");
  const now = Date.now();
  const hit = cache.get(key);
  if (hit && hit.until > now)
    return { protocolVersion: 1, serverTime: new Date().toISOString(), result: hit.result };

  const result = await routeFetch(request.origin, request.destination);
  if (cache.size >= MAX_CACHE) {
    const first = cache.keys().next().value;
    if (first) cache.delete(first);
  }
  cache.set(key, { until: now + CACHE_TTL_MS, result });
  return { protocolVersion: 1, serverTime: new Date().toISOString(), result };
}

/** Dependency injection and no persistent cache are intentional in this phase. */
export function clearLocalRoadPreviewCacheForTests(): void {
  cache.clear();
}

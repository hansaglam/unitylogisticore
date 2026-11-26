import { createHash } from "node:crypto";
import { CommandError } from "./commands";
import { getApprovedRoadCoordinate } from "./roadCoordinateCatalog";

export const ROAD_PREVIEW_VERSION = "lc_road_preview_v1";
export const ROAD_PREVIEW_PROVIDER = "local_osrm_driving";
export const LOCAL_OSRM_ROUTE_ORIGIN = "http://127.0.0.1:5055";
const MAX_POINTS = 4096;
const MAX_BODY_BYTES = 400000;
const MAX_SNAP_METERS = 30000;
const MAX_ROUTE_METERS = 12000000;
const MAX_ROUTE_SECONDS = 60 * 60 * 24 * 14;

export type RoadLocation = { countryCode: string; cityKey: string };
export type RoadCity = { location: RoadLocation; longitude: number; latitude: number };
export type RoadGeometry = readonly { latitude: number; longitude: number }[];
export type RoadPreview =
  | { available: false; reason: "no_road_route"; dispatchReady: false; purpose: "map_preview_only" }
  | { available: true; routeId: string; provider: typeof ROAD_PREVIEW_PROVIDER;
      mode: "road"; origin: RoadLocation; destination: RoadLocation;
      distanceMeters: number; durationSeconds: number; geometry: RoadGeometry;
      dispatchReady: false; purpose: "map_preview_only"; verification: "geometry_only";
      attribution: "© OpenStreetMap contributors"; };

const positive = (x: unknown, max: number) =>
  typeof x === "number" && Number.isFinite(x) && x > 0 && x <= max;
function haversine(a: { latitude: number; longitude: number },
  b: { latitude: number; longitude: number }) {
  const rad = Math.PI / 180, latDelta = (a.latitude - b.latitude) * rad;
  const lonDelta = (a.longitude - b.longitude) * rad;
  const h = Math.sin(latDelta / 2) ** 2 +
    Math.cos(a.latitude * rad) * Math.cos(b.latitude * rad) *
    Math.sin(lonDelta / 2) ** 2;
  return 12742000 * Math.asin(Math.min(1, Math.sqrt(h)));
}

function validPoint(point: unknown): point is [number, number] {
  return Array.isArray(point) && point.length === 2 &&
    typeof point[0] === "number" && Number.isFinite(point[0]) &&
    typeof point[1] === "number" && Number.isFinite(point[1]) &&
    point[0] >= -180 && point[0] <= 180 &&
    point[1] >= -85.0511287798066 && point[1] <= 85.0511287798066;
}

/**
 * Parses the official OSRM GeoJSON response. No fallback to a great-circle
 * line; a disconnected road network returns no_road_route. OSRM's standard
 * driving profile is NOT proof of heavy-goods compliance, ferry permissions
 * or freight contract eligibility.
 */
export function parseLocalOsrmRoute(payload: unknown, origin: RoadCity,
  destination: RoadCity): RoadPreview {
  if (!payload || typeof payload !== "object" || Array.isArray(payload))
    throw new CommandError("RETRY_LATER");
  const root = payload as Record<string, unknown>;
  if (root.code === "NoRoute" || root.code === "NoSegment")
    return { available: false, reason: "no_road_route",
      dispatchReady: false, purpose: "map_preview_only" };
  if (root.code !== "Ok" || !Array.isArray(root.routes) ||
      root.routes.length !== 1 || typeof root.routes[0] !== "object" ||
      root.routes[0] === null)
    throw new CommandError("RETRY_LATER");

  const route = root.routes[0] as Record<string, unknown>;
  const geo = route.geometry as Record<string, unknown> | null;
  if (!positive(route.distance, MAX_ROUTE_METERS) ||
      !positive(route.duration, MAX_ROUTE_SECONDS) ||
      !geo || geo.type !== "LineString" || !Array.isArray(geo.coordinates) ||
      geo.coordinates.length < 2 || geo.coordinates.length > MAX_POINTS ||
      !geo.coordinates.every(validPoint))
    throw new CommandError("RETRY_LATER");

  const coordinates = geo.coordinates as [number, number][];
  const points = coordinates.map(p => ({ longitude: p[0], latitude: p[1] }));
  if (haversine(origin, points[0]!) > MAX_SNAP_METERS ||
      haversine(destination, points[points.length - 1]!) > MAX_SNAP_METERS ||
      Number(route.distance) < haversine(origin, destination) * 0.8)
    throw new CommandError("RETRY_LATER");

  for (let i = 1; i < points.length; i++)
    if (Math.abs(points[i]!.longitude - points[i - 1]!.longitude) > 180)
      throw new CommandError("RETRY_LATER"); // antimeridian split not yet implemented

  const routeId = createHash("sha256")
    .update(JSON.stringify({ origin: origin.location, destination: destination.location,
      distance: route.distance, duration: route.duration, points }))
    .digest("hex").slice(0,40);
  return {
    available: true, routeId, provider: ROAD_PREVIEW_PROVIDER, mode: "road",
    origin: origin.location, destination: destination.location,
    distanceMeters: route.distance as number, durationSeconds: route.duration as number,
    geometry: points, dispatchReady: false, purpose: "map_preview_only",
    verification: "geometry_only", attribution: "© OpenStreetMap contributors",
  };
}

export type LocalRouteFetch = (url: string, options: {
  signal: AbortSignal; headers: Record<string, string>;
}) => Promise<{ ok: boolean; status: number; headers: { get(name: string): string | null };
  text(): Promise<string> }>;

export async function fetchLocalRoadPreview(originInput: unknown, destInput: unknown,
  transport: LocalRouteFetch = fetch): Promise<RoadPreview> {
  const origin = getApprovedRoadCoordinate(originInput);
  const destination = getApprovedRoadCoordinate(destInput);
  if (origin.location.countryCode === destination.location.countryCode &&
      origin.location.cityKey === destination.location.cityKey)
    throw new CommandError("INVALID_ARGUMENT");

  // FIXED loopback provider: no client URI, hostname or open proxy.
  // No commercial use of publicly hosted demo routing endpoints.
  const o = origin.longitude.toFixed(6) + "," + origin.latitude.toFixed(6);
  const d = destination.longitude.toFixed(6) + "," + destination.latitude.toFixed(6);
  const url = LOCAL_OSRM_ROUTE_ORIGIN + "/route/v1/driving/" + o + ";" + d +
    "?overview=simplified&geometries=geojson&steps=false&alternatives=false";
  try {
    const response = await transport(url, {
      signal: AbortSignal.timeout(4000),
      headers: { "Accept": "application/json" },
    });
    if (!response.ok) {
      if (response.status === 400 || response.status === 404)
        return { available: false, reason: "no_road_route",
          dispatchReady: false, purpose: "map_preview_only" };
      throw new CommandError("RETRY_LATER");
    }
    const length = response.headers.get("content-length");
    if (length !== null && (Number(length) > MAX_BODY_BYTES || Number(length) < 0))
      throw new CommandError("RETRY_LATER");
    const raw = await response.text();
    if (Buffer.byteLength(raw, "utf8") > MAX_BODY_BYTES)
      throw new CommandError("RETRY_LATER");
    return parseLocalOsrmRoute(JSON.parse(raw), origin, destination);
  } catch (error) {
    if (error instanceof CommandError) throw error;
    // Timeout, missing local OSRM, invalid JSON -> unavailable, not a fake road.
    throw new CommandError("RETRY_LATER");
  }
}

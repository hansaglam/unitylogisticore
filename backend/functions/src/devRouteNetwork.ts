import { invalid } from "./economyModel";
import { digest } from "./logisticsModel";
import { DISTANCE_PROVENANCE_LEGACY_APPROXIMATE, validateRouteNetwork } from "./routeNetworkModel";

/**
 * Transcribed from logisticoreyeni/src/data/routes.ts (read-only).
 * Distances are legacy_approximate gameplay km, not official road measurements.
 * Not a Firestore write.
 */
export const LOCAL_DEV_ROUTE_NETWORK_VERSION = "lc_dev_route_network_v1";

const CITY_IDS = [
  "izmir", "istanbul", "ankara", "bursa", "antalya", "adana", "diyarbakir", "trabzon",
] as const;

function edge(
  originCityId: string, destinationCityId: string, distance: number, difficultyMilli: number, tollCost: number,
) {
  return {
    id: `${originCityId}-${destinationCityId}`, originCityId, destinationCityId, distance, unit: "km",
    verification: "synthetic_fixture", difficultyMilli, tollCost,
  };
}

export function devRouteNetworkDocument(): Record<string, unknown> {
  return {
    schemaVersion: 1,
    version: LOCAL_DEV_ROUTE_NETWORK_VERSION,
    approval: "synthetic_emulator_only",
    cityIds: [...CITY_IDS],
    distanceUnit: "km",
    distanceProvenance: DISTANCE_PROVENANCE_LEGACY_APPROXIMATE,
    routes: [
      edge("izmir", "istanbul", 480, 82, 180),
      edge("istanbul", "izmir", 480, 85, 180),
      edge("izmir", "ankara", 585, 70, 120),
      edge("ankara", "izmir", 585, 72, 120),
      edge("izmir", "bursa", 410, 65, 90),
      edge("bursa", "izmir", 410, 65, 90),
      edge("izmir", "antalya", 455, 55, 75),
      edge("antalya", "izmir", 455, 58, 75),
      edge("istanbul", "ankara", 450, 88, 200),
      edge("ankara", "istanbul", 450, 90, 200),
      edge("istanbul", "bursa", 155, 92, 150),
      edge("bursa", "istanbul", 155, 90, 150),
      edge("istanbul", "antalya", 715, 80, 220),
      edge("antalya", "istanbul", 715, 82, 220),
      edge("ankara", "bursa", 385, 68, 85),
      edge("bursa", "ankara", 385, 70, 85),
      edge("ankara", "antalya", 545, 62, 95),
      edge("antalya", "ankara", 545, 65, 95),
      edge("bursa", "antalya", 660, 72, 110),
      edge("antalya", "bursa", 660, 74, 110),
      edge("adana", "ankara", 550, 65, 100),
      edge("ankara", "adana", 550, 67, 100),
      edge("adana", "antalya", 480, 58, 85),
      edge("antalya", "adana", 480, 60, 85),
      edge("adana", "istanbul", 940, 78, 210),
      edge("istanbul", "adana", 940, 80, 210),
      edge("adana", "izmir", 870, 72, 175),
      edge("izmir", "adana", 870, 74, 175),
      edge("adana", "bursa", 820, 70, 165),
      edge("bursa", "adana", 820, 72, 165),
      edge("adana", "diyarbakir", 540, 62, 95),
      edge("diyarbakir", "adana", 540, 64, 95),
      edge("adana", "trabzon", 680, 68, 120),
      edge("trabzon", "adana", 680, 70, 120),
      edge("diyarbakir", "ankara", 980, 70, 165),
      edge("ankara", "diyarbakir", 980, 72, 165),
      edge("diyarbakir", "istanbul", 1280, 82, 240),
      edge("istanbul", "diyarbakir", 1280, 84, 240),
      edge("diyarbakir", "izmir", 1180, 76, 210),
      edge("izmir", "diyarbakir", 1180, 78, 210),
      edge("diyarbakir", "bursa", 1100, 74, 195),
      edge("bursa", "diyarbakir", 1100, 76, 195),
      edge("diyarbakir", "antalya", 850, 68, 145),
      edge("antalya", "diyarbakir", 850, 70, 145),
      edge("diyarbakir", "trabzon", 520, 64, 90),
      edge("trabzon", "diyarbakir", 520, 66, 90),
      edge("trabzon", "ankara", 760, 72, 155),
      edge("ankara", "trabzon", 760, 74, 155),
      edge("trabzon", "istanbul", 1080, 80, 225),
      edge("istanbul", "trabzon", 1080, 82, 225),
      edge("trabzon", "izmir", 1180, 78, 215),
      edge("izmir", "trabzon", 1180, 80, 215),
      edge("trabzon", "bursa", 920, 74, 185),
      edge("bursa", "trabzon", 920, 76, 185),
      edge("trabzon", "antalya", 920, 76, 190),
      edge("antalya", "trabzon", 920, 78, 190),
    ],
  };
}

export function validateDevRouteNetwork(data: unknown = devRouteNetworkDocument()) {
  return validateRouteNetwork(data as Parameters<typeof validateRouteNetwork>[0], LOCAL_DEV_ROUTE_NETWORK_VERSION);
}

export function devRouteNetworkDigest(): string {
  return digest(validateDevRouteNetwork());
}

export function contractRouteFromNetwork(routeId: string) {
  const route = validateDevRouteNetwork().routes.find(r => r.id === routeId);
  if (!route) invalid();
  return {
    id: route.id, originCityId: route.originCityId, destinationCityId: route.destinationCityId,
    distance: route.distance, unit: route.unit, verification: route.verification,
  };
}

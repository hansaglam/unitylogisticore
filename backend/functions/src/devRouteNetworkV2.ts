import { invalid } from "./economyModel";
import { digest } from "./logisticsModel";
import {
  DISTANCE_PROVENANCE_KGM_IL_MERKEZ_2026_03_03, ROUTE_VERIFICATION_KGM_ILMESAFE, validateRouteNetwork,
} from "./routeNetworkModel";

/**
 * Official KGM il-merkezi km (workbook 2026-03-03, SHA-256 9202ff47…).
 * Not warehouse routing, truck navigation, or live guidance.
 * Not a Firestore write. Does not replace lc_dev_route_network_v1.
 */
export const LOCAL_DEV_ROUTE_NETWORK_V2 = "lc_dev_route_network_v2";

const CITY_IDS = [
  "izmir", "istanbul", "ankara", "bursa", "antalya", "adana", "diyarbakir", "trabzon",
] as const;

function edge(
  originCityId: string, destinationCityId: string, distance: number, difficultyMilli: number, tollCost: number,
) {
  return {
    id: `${originCityId}-${destinationCityId}`, originCityId, destinationCityId, distance, unit: "km",
    verification: ROUTE_VERIFICATION_KGM_ILMESAFE, difficultyMilli, tollCost,
  };
}

export function devRouteNetworkV2Document(): Record<string, unknown> {
  return {
    schemaVersion: 1,
    version: LOCAL_DEV_ROUTE_NETWORK_V2,
    approval: "synthetic_emulator_only",
    cityIds: [...CITY_IDS],
    distanceUnit: "km",
    distanceProvenance: DISTANCE_PROVENANCE_KGM_IL_MERKEZ_2026_03_03,
    routes: [
      edge("izmir", "istanbul", 566, 82, 180),
      edge("istanbul", "izmir", 566, 85, 180),
      edge("izmir", "ankara", 586, 70, 120),
      edge("ankara", "izmir", 586, 72, 120),
      edge("izmir", "bursa", 325, 65, 90),
      edge("bursa", "izmir", 325, 65, 90),
      edge("izmir", "antalya", 447, 55, 75),
      edge("antalya", "izmir", 447, 58, 75),
      edge("istanbul", "ankara", 453, 88, 200),
      edge("ankara", "istanbul", 453, 90, 200),
      edge("istanbul", "bursa", 244, 92, 150),
      edge("bursa", "istanbul", 244, 90, 150),
      edge("istanbul", "antalya", 715, 80, 220),
      edge("antalya", "istanbul", 715, 82, 220),
      edge("ankara", "bursa", 385, 68, 85),
      edge("bursa", "ankara", 385, 70, 85),
      edge("ankara", "antalya", 542, 62, 95),
      edge("antalya", "ankara", 542, 65, 95),
      edge("bursa", "antalya", 543, 72, 110),
      edge("antalya", "bursa", 543, 74, 110),
      edge("adana", "ankara", 491, 65, 100),
      edge("ankara", "adana", 491, 67, 100),
      edge("adana", "antalya", 550, 58, 85),
      edge("antalya", "adana", 550, 60, 85),
      edge("adana", "istanbul", 951, 78, 210),
      edge("istanbul", "adana", 951, 80, 210),
      edge("adana", "izmir", 909, 72, 175),
      edge("izmir", "adana", 909, 74, 175),
      edge("adana", "bursa", 856, 70, 165),
      edge("bursa", "adana", 856, 72, 165),
      edge("adana", "diyarbakir", 537, 62, 95),
      edge("diyarbakir", "adana", 537, 64, 95),
      edge("adana", "trabzon", 832, 68, 120),
      edge("trabzon", "adana", 832, 70, 120),
      edge("diyarbakir", "ankara", 898, 70, 165),
      edge("ankara", "diyarbakir", 898, 72, 165),
      edge("diyarbakir", "istanbul", 1349, 82, 240),
      edge("istanbul", "diyarbakir", 1349, 84, 240),
      edge("diyarbakir", "izmir", 1429, 76, 210),
      edge("izmir", "diyarbakir", 1429, 78, 210),
      edge("diyarbakir", "bursa", 1270, 74, 195),
      edge("bursa", "diyarbakir", 1270, 76, 195),
      edge("diyarbakir", "antalya", 1087, 68, 145),
      edge("antalya", "diyarbakir", 1087, 70, 145),
      edge("diyarbakir", "trabzon", 610, 64, 90),
      edge("trabzon", "diyarbakir", 610, 66, 90),
      edge("trabzon", "ankara", 733, 72, 155),
      edge("ankara", "trabzon", 733, 74, 155),
      edge("trabzon", "istanbul", 1057, 80, 225),
      edge("istanbul", "trabzon", 1057, 82, 225),
      edge("trabzon", "izmir", 1328, 78, 215),
      edge("izmir", "trabzon", 1328, 80, 215),
      edge("trabzon", "bursa", 1065, 74, 185),
      edge("bursa", "trabzon", 1065, 76, 185),
      edge("trabzon", "antalya", 1224, 76, 190),
      edge("antalya", "trabzon", 1224, 78, 190),
    ],
  };
}

export function validateDevRouteNetworkV2(data: unknown = devRouteNetworkV2Document()) {
  return validateRouteNetwork(data as Parameters<typeof validateRouteNetwork>[0], LOCAL_DEV_ROUTE_NETWORK_V2);
}

export function devRouteNetworkV2Digest(): string {
  return digest(validateDevRouteNetworkV2());
}

export function contractRouteFromNetworkV2(routeId: string) {
  const route = validateDevRouteNetworkV2().routes.find(r => r.id === routeId);
  if (!route) invalid();
  return {
    id: route.id, originCityId: route.originCityId, destinationCityId: route.destinationCityId,
    distance: route.distance, unit: route.unit, verification: route.verification,
  };
}

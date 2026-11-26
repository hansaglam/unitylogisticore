import test from "node:test";
import assert from "node:assert/strict";
import {
  fetchLocalRoadPreview, parseLocalOsrmRoute, LOCAL_OSRM_ROUTE_ORIGIN,
} from "../functions/lib/localRoadRouting.js";
import {
  getApprovedRoadCoordinate, roadCoordinateCount,
} from "../functions/lib/roadCoordinateCatalog.js";
import { validateRoadPreviewRequest } from "../functions/lib/roadPreview.js";

const errorIs = code => error => error.details?.code === code;
const ankara = { countryCode: "TUR", cityKey: "ankara" };
const istanbul = { countryCode: "TUR", cityKey: "istanbul" };
const berlin = { countryCode: "DEU", cityKey: "ne-q64" };

const roadResponse = () => ({
  code: "Ok",
  routes: [{
    distance: 453000,
    duration: 22300,
    // SYNTHETIC MOCK DATA ONLY, never an in-game route or routing fixture.
    geometry: {
      type: "LineString",
      coordinates: [[32.862446, 39.929184], [31.25, 40.55], [28.974277, 41.017602]],
    },
  }],
});

test("248 route-capable *catalog points* exactly match the pinned launcher city IDs", () => {
  assert.equal(roadCoordinateCount(), 248);
  assert.deepEqual(getApprovedRoadCoordinate(ankara), {
    location: ankara, longitude: 32.862446, latitude: 39.929184,
  });
  assert.equal(getApprovedRoadCoordinate(berlin).location.cityKey, berlin.cityKey);
  assert.throws(() => getApprovedRoadCoordinate({countryCode:"USA", cityKey:"ne-q60"}),
    errorIs("INVALID_ARGUMENT"));
  assert.throws(() => getApprovedRoadCoordinate({countryCode:"TUR",cityKey:"unknown"}),
    errorIs("INVALID_ARGUMENT"));
  assert.throws(() => validateRoadPreviewRequest({protocolVersion:1, origin:ankara,destination:ankara}),
    errorIs("INVALID_ARGUMENT"));
  assert.throws(() => validateRoadPreviewRequest({protocolVersion:1, origin:ankara,
    destination:istanbul, discount: 50}), errorIs("INVALID_ARGUMENT"));
});

test("real provider response contract produces geometry-only non-payable route", () => {
  const a = getApprovedRoadCoordinate(ankara);
  const b = getApprovedRoadCoordinate(istanbul);
  const quote = parseLocalOsrmRoute(roadResponse(), a, b);
  assert.equal(quote.available, true);
  assert.equal(quote.provider, "local_osrm_driving");
  assert.equal(quote.mode, "road");
  assert.equal(quote.distanceMeters, 453000);
  assert.equal(quote.durationSeconds, 22300);
  assert.equal(quote.dispatchReady, false);
  assert.equal(quote.purpose, "map_preview_only");
  assert.equal(quote.verification, "geometry_only");
  assert.equal(quote.attribution, "© OpenStreetMap contributors");
  assert.equal(quote.routeId.length, 40);
  assert.equal(quote.geometry.length, 3);
  assert.deepEqual(parseLocalOsrmRoute({code:"NoRoute"}, a, b),
    { available: false, reason: "no_road_route", dispatchReady: false,
      purpose: "map_preview_only" });
});

test("malformed, disconnected, wrong-endpoint and huge geometry fail closed", () => {
  const a = getApprovedRoadCoordinate(ankara);
  const b = getApprovedRoadCoordinate(istanbul);
  for (const altered of [
    { ...roadResponse(), routes: [] },
    { ...roadResponse(), code: "Unknown" },
    { ...roadResponse(), routes: [{
      ...roadResponse().routes[0], distance: -5,
    }] },
    { ...roadResponse(), routes: [{
      ...roadResponse().routes[0], duration: Number.POSITIVE_INFINITY,
    }] },
    { ...roadResponse(), routes: [{
      ...roadResponse().routes[0], geometry: {
        type: "LineString", coordinates: [[-70,40],[-71,41]],
      },
    }] },
    { ...roadResponse(), routes: [{
      ...roadResponse().routes[0], geometry: {
        type: "LineString", coordinates: Array.from({length:4097}, () => [32.8,39.9]),
      },
    }] },
    { ...roadResponse(), routes: [{
      ...roadResponse().routes[0], geometry: {
        type: "LineString", coordinates: [[200,39],[201,41]],
      },
    }] },
  ]) assert.throws(() => parseLocalOsrmRoute(altered,a,b), errorIs("RETRY_LATER"));
});

test("only fixed localhost OSRM URI is called; no public demo or invented fallback", async () => {
  let seen = "";
  const fake = async (url, opts) => {
    seen = url;
    assert.ok(opts.signal);
    return { ok:true,status:200,headers:{get:()=>null},
      text:async()=>JSON.stringify(roadResponse()) };
  };
  const route = await fetchLocalRoadPreview(ankara, istanbul, fake);
  assert.equal(route.available, true);
  assert.ok(seen.startsWith(LOCAL_OSRM_ROUTE_ORIGIN+"/route/v1/driving/"));
  assert.match(seen, /geometries=geojson/);
  assert.doesNotMatch(seen, /router.project-osrm.org|localhost:3000/);

  const noRoad = await fetchLocalRoadPreview(ankara, istanbul,
    async()=>({ok:true,status:200,headers:{get:()=>null},
      text:async()=>JSON.stringify({code:"NoRoute"})}));
  assert.equal(noRoad.available, false);
  await assert.rejects(fetchLocalRoadPreview(ankara,istanbul,
    async()=>{throw new Error("routing engine offline");}), errorIs("RETRY_LATER"));
  await assert.rejects(fetchLocalRoadPreview(ankara,istanbul,
    async()=>({ok:true,status:200,headers:{get:()=> "5000000"},text:async()=>""})),
    errorIs("RETRY_LATER"));
});

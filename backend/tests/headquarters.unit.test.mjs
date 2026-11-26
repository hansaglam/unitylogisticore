import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import {
  HEADQUARTERS_CATALOG_VERSION, initialHeadquartersCount,
  validateInitialHeadquarters,
} from "../functions/lib/headquartersCatalog.js";
import {
  validateCreateCompanyWithHeadquartersPayload,
  validateHeadquartersRecord,
} from "../functions/lib/headquarters.js";
import { validateCompanyPayload } from "../functions/lib/company.js";
import {
  STARTER_SITE_DISPATCH, STARTER_SITE_DOCUMENT, STARTER_SITE_STATUS,
  validateStarterSiteRecord,
} from "../functions/lib/starterDepotSite.js";

import { Timestamp } from "firebase-admin/firestore";

const errorIs = code => error => error.details?.code === code;

test("initial HQ catalog matches C# region exactly, without user-supplied locations", () => {
  const citiesText = readFileSync(fileURLToPath(
    new URL("../../Assets/Game/Map/MapGlobalCityCatalog.cs", import.meta.url)), "utf8");
  const regionText = readFileSync(fileURLToPath(
    new URL("../../Assets/Game/Map/MapTradeRegionCatalog.cs", import.meta.url)), "utf8");
  const countriesBlock = regionText.split("static readonly HashSet<string> Countries")[1].split("};")[0];
  const countries = new Set([...countriesBlock.matchAll(/"([A-Z]{3})"/g)].map(m => m[1]));
  const cities = [...citiesText.matchAll(
    /new MapGlobalCity\("([A-Z]{3})", "([^"]+)", "([^"]+)", ([-\d.]+)d, ([-\d.]+)d, (\d+)\)/g,
  )].map(m => ({ countryCode: m[1], cityKey: m[2], latitude: +m[4], longitude: +m[5] }));
  const inScope = city => {
    if (!countries.has(city.countryCode)) return false;
    if (city.countryCode === "TUR") return true;
    if (city.countryCode === "RUS")
      return city.longitude >= 19 && city.longitude <= 60 &&
        city.latitude >= 41 && city.latitude <= 72;
    return city.longitude >= -32 && city.longitude <= 60 &&
      city.latitude >= 30 && city.latitude <= 72;
  };

  const expected = cities.filter(inScope);
  assert.equal(expected.length, 248);
  assert.equal(expected.filter(c => c.countryCode === "TUR").length, 81);
  assert.equal(initialHeadquartersCount(), expected.length);
  for (const city of expected)
    assert.deepEqual(validateInitialHeadquarters({
      countryCode: city.countryCode, cityKey: city.cityKey,
    }), { countryCode: city.countryCode, cityKey: city.cityKey });

  for (const city of cities.filter(c => !inScope(c)))
    assert.throws(() => validateInitialHeadquarters({
      countryCode: city.countryCode, cityKey: city.cityKey,
    }), errorIs("INVALID_ARGUMENT"));
  assert.equal(HEADQUARTERS_CATALOG_VERSION, "lc_tur_europe_2026_11_v1");
});

test("HQ request validates exact names and vetted canonical city keys", () => {
  const old = validateCompanyPayload({ name: "  İstanbul Lojistik  " });
  assert.deepEqual(old, { name: "İstanbul Lojistik" });

  const good = { name: "  İstanbul Lojistik  ",
    headquarters: { countryCode: "TUR", cityKey: "ankara" } };
  assert.deepEqual(validateCreateCompanyWithHeadquartersPayload(good),
    { name: "İstanbul Lojistik", headquarters: good.headquarters });
  const invalidLocations = [
    { countryCode: "USA", cityKey: "ankara" },
    { countryCode: "TUR", cityKey: "unknown" },
    { countryCode: "tur", cityKey: "ankara" },
    { countryCode: "TUR", cityKey: "Ankara" },
    { countryCode: "TUR", cityKey: "ankara", lat: 39 },
    { countryCode: "TUR", cityKey: "../ankara" }, {}, null,
  ];
  for (const headquarters of invalidLocations)
    assert.throws(() => validateCreateCompanyWithHeadquartersPayload(
      { name: "Synthetic Logistics", headquarters }), errorIs("INVALID_ARGUMENT"));

  for (const payload of [
    { ...good, balance: 999999 },
    { ...good, ownerUid: "other" },
    { ...good, companyId: "foreign-company" },
    { name: "Synthetic Logistics" }, { headquarters: good.headquarters },
  ])
    assert.throws(() => validateCreateCompanyWithHeadquartersPayload(payload),
      errorIs("INVALID_ARGUMENT"));
  assert.throws(() => validateCompanyPayload(good), errorIs("INVALID_ARGUMENT"));
});

test("only authenticated matching immutable HQ sidecars are readable", () => {
  const state = {
    schemaVersion: 1, companyId: "co_test", ownerUid: "user_test",
    catalogVersion: HEADQUARTERS_CATALOG_VERSION,
    countryCode: "TUR", cityKey: "ankara",
    selectionKind: "initial_company_setup", selectedAt: Timestamp.now(),
  };
  assert.deepEqual(validateHeadquartersRecord(state, "user_test", "co_test"), {
    countryCode: "TUR", cityKey: "ankara",
    catalogVersion: HEADQUARTERS_CATALOG_VERSION,
  });
  for (const changed of [
    { ownerUid: "other" }, { companyId: "other" }, { schemaVersion: 2 },
    { cityKey: "unknown" }, { catalogVersion: "unapproved" },
    { selectionKind: "moved" }, { selectedAt: "fake" }, { balance: 900 },
  ])
    assert.throws(() => validateHeadquartersRecord({ ...state, ...changed }, "user_test", "co_test"),
      errorIs("COMPANY_STATE_INVALID"));
});


test("reserved initial depot site is immutable, location-bound and never dispatch-ready", () => {
  assert.equal(STARTER_SITE_DOCUMENT, "starterDepotSite");
  assert.equal(STARTER_SITE_STATUS, "site_reserved");
  assert.equal(STARTER_SITE_DISPATCH, "unavailable_pending_verified_routes");
  const hq = { countryCode: "TUR", cityKey: "ankara" };
  const site = {
    schemaVersion: 1, companyId: "co_test", ownerUid: "owner_test",
    countryCode: hq.countryCode, cityKey: hq.cityKey,
    catalogVersion: HEADQUARTERS_CATALOG_VERSION,
    status: STARTER_SITE_STATUS, dispatchStatus: STARTER_SITE_DISPATCH,
    source: "initial_company_setup", reservedAt: Timestamp.now(),
  };
  assert.deepEqual(validateStarterSiteRecord(site, "owner_test", "co_test", hq), {
    countryCode: "TUR", cityKey: "ankara",
    catalogVersion: HEADQUARTERS_CATALOG_VERSION,
    status: "site_reserved", dispatchStatus: "unavailable_pending_verified_routes",
  });
  const forged = [
    { companyId: "other" }, { ownerUid: "other" },
    { cityKey: "istanbul" }, { countryCode: "DEU" },
    { status: "operational" }, { dispatchStatus: "ready" },
    { catalogVersion: "unknown" }, { source: "client" },
    { reservedAt: "spoof" }, { cash: 999999 },
  ];
  for (const change of forged)
    assert.throws(() => validateStarterSiteRecord(
      { ...site, ...change }, "owner_test", "co_test", hq),
      errorIs("COMPANY_STATE_INVALID"));
});

import test from "node:test";
import assert from "node:assert/strict";
import { assertLocalEnvironment, LOCAL_PROJECT } from "../functions/lib/localEnvironment.js";

assertLocalEnvironment(process.env);
const { initializeApp, deleteApp } = await import("firebase/app");
const { getAuth, connectAuthEmulator, signInAnonymously, deleteUser } =
  await import("firebase/auth");
const { createLocalFirestore } = await import("../functions/lib/identity.js");
const { companyIdForUid } = await import("../functions/lib/company.js");
const { Timestamp } = await import("firebase-admin/firestore");

async function call(user, name, data) {
  const endpoint = "http://127.0.0.1:5001/" + LOCAL_PROJECT + "/us-central1/" + name;
  const token = user ? await user.getIdToken() : undefined;
  const response = await fetch(endpoint, {
    method: "POST", redirect: "error",
    signal: AbortSignal.timeout(20000),
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: "Bearer " + token } : {}),
    },
    body: JSON.stringify({ data }),
  });
  return { status: response.status, body: await response.json() };
}

function errorIs(outcome, code) {
  assert.notEqual(outcome.status, 200);
  assert.equal(outcome.body.error.details.code, code);
}

test("CITY-START-02: atomic HQ creation, replay and legacy company preservation", {
  timeout: 180000,
}, async () => {
  const oldRuntime = process.env.FUNCTIONS_EMULATOR;
  process.env.FUNCTIONS_EMULATOR = "true";
  const db = createLocalFirestore();
  const apps = [];
  const users = [];
  const refs = new Map();
  const track = path => {
    const ref = db.doc(path);
    refs.set(ref.path, ref);
    return ref;
  };
  const guest = async () => {
    const app = initializeApp({ projectId: LOCAL_PROJECT, apiKey: "synthetic-key" },
      "hq-test-" + Date.now() + "-" + apps.length);
    apps.push(app);
    const auth = getAuth(app);
    connectAuthEmulator(auth, "http://127.0.0.1:9099", { disableWarnings: true });
    const user = (await signInAnonymously(auth)).user;
    users.push(user);
    track("v2Players/" + user.uid);
    track("v2Companies/" + companyIdForUid(user.uid));
    return user;
  };
  const read = user => call(user, "getMyCompanyHeadquarters", { protocolVersion: 1 });
  const readSite = user => call(user, "getMyStarterDepotSite", { protocolVersion: 1 });
  const create = (user, key, countryCode = "TUR", cityKey = "ankara") =>
    call(user, "createCompanyWithHeadquarters", {
      protocolVersion: 1, idempotencyKey: key,
      payload: { name: "Synthetic Logistics", headquarters: { countryCode, cityKey } },
    });

  try {
    assert.equal((await read()).status, 401);
    const user = await guest();
    errorIs(await read(user), "FAILED_PRECONDITION");
    assert.equal((await call(user, "ensurePlayerIdentity", { protocolVersion: 1 })).status, 200);
    assert.deepEqual((await read(user)).body.result.result,
      { hasCompany: false, hasHeadquarters: false });

    const companyRef = track("v2Companies/" + companyIdForUid(user.uid));
    const hqRef = track(companyRef.path + "/state/headquarters");
    const starterSiteRef = track(companyRef.path + "/state/starterDepotSite");
    const receipt = track("v2Players/" + user.uid + "/commands/hq-initial");
    const created = await create(user, "hq-initial");
    assert.equal(created.status, 200);
    assert.equal(created.body.result.result.headquarters.countryCode, "TUR");
    assert.equal(created.body.result.result.headquarters.cityKey, "ankara");
    assert.deepEqual(created.body.result.result.starterSite, {
      countryCode: "TUR", cityKey: "ankara", status: "site_reserved",
      dispatchStatus: "unavailable_pending_verified_routes",
    });

    const storedCompany = (await companyRef.get()).data();
    assert.deepEqual(Object.keys(storedCompany).sort(),
      ["bootstrapStatus", "createdAt", "name", "ownerUid", "schemaVersion"]);
    assert.equal(storedCompany.schemaVersion, 1);
    assert.equal(storedCompany.ownerUid, user.uid);
    const storedHq = (await hqRef.get()).data();
    assert.deepEqual(Object.keys(storedHq).sort(),
      ["catalogVersion", "cityKey", "companyId", "countryCode", "ownerUid",
        "schemaVersion", "selectedAt", "selectionKind"]);
    assert.ok(storedHq.selectedAt instanceof Timestamp);
    assert.equal(storedHq.ownerUid, user.uid);
    assert.equal(storedHq.companyId, companyRef.id);
    assert.equal(storedHq.catalogVersion, "lc_tur_europe_2026_11_v1");
    const storedSite = (await starterSiteRef.get()).data();
    assert.deepEqual(Object.keys(storedSite).sort(), [
      "catalogVersion", "cityKey", "companyId", "countryCode", "dispatchStatus",
      "ownerUid", "reservedAt", "schemaVersion", "source", "status",
    ]);
    assert.equal(storedSite.companyId, companyRef.id);
    assert.equal(storedSite.ownerUid, user.uid);
    assert.equal(storedSite.cityKey, "ankara");
    assert.equal(storedSite.status, "site_reserved");
    assert.equal(storedSite.dispatchStatus, "unavailable_pending_verified_routes");
    assert.ok(storedSite.reservedAt instanceof Timestamp);
    assert.deepEqual((await readSite(user)).body.result.result, {
      hasCompany: true, hasHeadquarters: true, hasStarterSite: true,
      companyId: companyRef.id,
      starterSite: {
        countryCode: "TUR", cityKey: "ankara",
        catalogVersion: "lc_tur_europe_2026_11_v1",
        status: "site_reserved",
        dispatchStatus: "unavailable_pending_verified_routes",
      },
    });

    const readHq = await read(user);
    assert.equal(readHq.status, 200);
    assert.deepEqual(readHq.body.result.result, {
      hasCompany: true, hasHeadquarters: true, companyId: companyRef.id,
      headquarters: {
        countryCode: "TUR", cityKey: "ankara",
        catalogVersion: "lc_tur_europe_2026_11_v1",
      },
    });
    const legacyRead = await call(user, "getMyCompanyState", { protocolVersion: 1 });
    assert.equal(legacyRead.status, 200);
    assert.deepEqual(Object.keys(legacyRead.body.result.result).sort(),
      ["bootstrapStatus", "companyId", "economyReady", "hasCompany", "name", "schemaVersion"]);
    const stableCompany = await companyRef.get();
    const stableHq = await hqRef.get();
    const stableSite = await starterSiteRef.get();
    const stableReceipt = await receipt.get();

    assert.deepEqual((await create(user, "hq-initial")).body, created.body);
    errorIs(await create(user, "hq-initial", "TUR", "istanbul"), "IDEMPOTENCY_CONFLICT");
    errorIs(await create(user, "second-key"), "COMPANY_EXISTS");
    errorIs(await call(user, "createCompany", {
      protocolVersion: 1, idempotencyKey: "old-after-new",
      payload: { name: "Synthetic Logistics" },
    }), "COMPANY_EXISTS");
    assert.deepEqual((await companyRef.get()).updateTime, stableCompany.updateTime);
    assert.deepEqual((await hqRef.get()).updateTime, stableHq.updateTime);
    assert.deepEqual((await starterSiteRef.get()).updateTime, stableSite.updateTime);
    assert.deepEqual((await receipt.get()).updateTime, stableReceipt.updateTime);

    const legacy = await guest();
    assert.equal((await call(legacy, "ensurePlayerIdentity", { protocolVersion: 1 })).status, 200);
    const legacyCompanyRef = track("v2Companies/" + companyIdForUid(legacy.uid));
    const legacyHq = track(legacyCompanyRef.path + "/state/headquarters");
    const legacySite = track(legacyCompanyRef.path + "/state/starterDepotSite");
    track("v2Players/" + legacy.uid + "/commands/old-company");
    assert.equal((await call(legacy, "createCompany", {
      protocolVersion: 1, idempotencyKey: "old-company",
      payload: { name: "Legacy Co" },
    })).status, 200);
    assert.deepEqual((await read(legacy)).body.result.result,
      { hasCompany: true, hasHeadquarters: false });
    errorIs(await create(legacy, "new-after-legacy"), "COMPANY_EXISTS");
    assert.equal((await legacyHq.get()).exists, false);
    assert.equal((await legacySite.get()).exists, false);
    assert.deepEqual((await readSite(legacy)).body.result.result,
      { hasCompany: true, hasHeadquarters: false, hasStarterSite: false });
    assert.equal((await legacyCompanyRef.get()).data().name, "Legacy Co");

    const missing = await guest();
    errorIs(await create(missing, "identity-missing"), "FAILED_PRECONDITION");
    assert.equal((await db.doc("v2Companies/" + companyIdForUid(missing.uid)).get()).exists, false);

    const bad = await guest();
    assert.equal((await call(bad, "ensurePlayerIdentity", { protocolVersion: 1 })).status, 200);
    for (const [i, location] of [
      ["USA", "ne-q60"], ["DEU", "fake"], ["TUR", "ankara/../test"],
    ].entries()) {
      errorIs(await create(bad, "bad-location-" + i, location[0], location[1]),
        "INVALID_ARGUMENT");
    }
    errorIs(await call(bad, "createCompanyWithHeadquarters", {
      protocolVersion: 1, idempotencyKey: "spoof",
      payload: { name: "New Co", headquarters: { countryCode: "TUR", cityKey: "ankara" },
        balance: 10000 },
    }), "INVALID_ARGUMENT");
    assert.equal((await db.doc("v2Companies/" + companyIdForUid(bad.uid)).get()).exists, false);
  } finally {
    // Explicitly delete sidecars and receipts; deleting a parent document
    // alone never deletes Firestore subcollections.
    const cleaned = await Promise.allSettled([...refs.values()].map(ref => ref.delete()));
    const removed = await Promise.allSettled(users.map(user => deleteUser(user)));
    await Promise.all(apps.map(app => deleteApp(app)));
    await db.terminate();
    if (oldRuntime === undefined) delete process.env.FUNCTIONS_EMULATOR;
    else process.env.FUNCTIONS_EMULATOR = oldRuntime;
    for (const outcome of [...cleaned, ...removed])
      assert.equal(outcome.status, "fulfilled");
  }
});

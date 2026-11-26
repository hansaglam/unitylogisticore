import test from "node:test";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { assertLocalEnvironment } from "../functions/lib/localEnvironment.js";

assertLocalEnvironment(process.env); // No production project/credentials allowed.
const { createLocalFirestore, initializeIdentity } = await import("../functions/lib/identity.js");
const { companyIdForUid, createCompanyCommand } = await import("../functions/lib/company.js");
const { createCompanyWithHeadquartersCommand } = await import("../functions/lib/headquarters.js");
const { clearLocalRoadPreviewCacheForTests,
  readMyRoadRoutePreview } = await import("../functions/lib/roadPreview.js");

const ankara = {countryCode:"TUR",cityKey:"ankara"};
const istanbul = {countryCode:"TUR",cityKey:"istanbul"};
const req = (origin=ankara,destination=istanbul) => ({
  protocolVersion:1,origin,destination,
});
const createReq = (key,payload) => ({
  protocolVersion:1,idempotencyKey:key,payload,
});
const errorIs = code => error => error.details?.code === code;

test("road preview is read-only and only starts from the authenticated company's HQ", {
  timeout: 120000,
}, async () => {
  const prev = process.env.FUNCTIONS_EMULATOR;
  process.env.FUNCTIONS_EMULATOR = "true";
  const db = createLocalFirestore();
  const uid = "synthetic-road-preview-" + randomUUID();
  const auth = {uid};
  const companyId = companyIdForUid(uid);
  const player = db.doc("v2Players/"+uid);
  const company = db.doc("v2Companies/"+companyId);
  const hq = company.collection("state").doc("headquarters");
  const site = company.collection("state").doc("starterDepotSite");
  const receipt = player.collection("commands").doc("road-hq-company");
  const tracked = [receipt,site,hq,company,player];
  clearLocalRoadPreviewCacheForTests();
  let providerCalls = 0;
  const provider = async(origin,destination)=>{
    providerCalls++;
    assert.deepEqual(origin,ankara);
    assert.deepEqual(destination,istanbul);
    return {available:false,reason:"no_road_route",dispatchReady:false,
      purpose:"map_preview_only"};
  };
  try {
    await assert.rejects(readMyRoadRoutePreview(db,undefined,req(),provider),
      errorIs("UNAUTHENTICATED"));
    await initializeIdentity(db,auth,{protocolVersion:1});
    await assert.rejects(readMyRoadRoutePreview(db,auth,req(),provider),
      errorIs("FAILED_PRECONDITION"));
    assert.equal(providerCalls,0);

    await createCompanyWithHeadquartersCommand(db,auth,createReq("road-hq-company",{
      name:"Synthetic Road Company",headquarters:ankara,
    }));
    const fromHQ = await readMyRoadRoutePreview(db,auth,req(),provider);
    assert.deepEqual(fromHQ.result,{available:false,reason:"no_road_route",
      dispatchReady:false,purpose:"map_preview_only"});
    assert.equal(providerCalls,1);
    await readMyRoadRoutePreview(db,auth,req(),provider);
    assert.equal(providerCalls,1); // short bounded public geometry cache
    await assert.rejects(
      readMyRoadRoutePreview(db,auth,req(istanbul,ankara),provider),
      errorIs("PERMISSION_DENIED"));
    await assert.rejects(
      readMyRoadRoutePreview(db,auth,req(ankara,
        {countryCode:"USA",cityKey:"ne-q60"}),provider),
      errorIs("INVALID_ARGUMENT"));
    assert.equal(providerCalls,1);
    assert.equal((await site.get()).data().status,"site_reserved");
    assert.equal((await hq.get()).data().cityKey,"ankara");
    assert.equal((await company.get()).data().name,"Synthetic Road Company");

    // Existing legacy account is not assigned a guessed route origin.
    const legacyUid = "synthetic-road-legacy-" + randomUUID();
    const legacyAuth = {uid:legacyUid};
    const legacyCompany = db.doc("v2Companies/"+companyIdForUid(legacyUid));
    const legacyPlayer = db.doc("v2Players/"+legacyUid);
    const legacyReceipt = legacyPlayer.collection("commands").doc("legacy-co");
    tracked.push(legacyReceipt,legacyCompany,legacyPlayer);
    await initializeIdentity(db,legacyAuth,{protocolVersion:1});
    await createCompanyCommand(db,legacyAuth,createReq("legacy-co",{name:"Existing Co"}));
    await assert.rejects(readMyRoadRoutePreview(db,legacyAuth,req(),provider),
      errorIs("FAILED_PRECONDITION"));
    assert.equal(providerCalls,1);
  } finally {
    const clean = await Promise.allSettled(tracked.map(ref=>ref.delete()));
    await db.terminate();
    clearLocalRoadPreviewCacheForTests();
    if(prev===undefined) delete process.env.FUNCTIONS_EMULATOR;
    else process.env.FUNCTIONS_EMULATOR=prev;
    for(const outcome of clean) assert.equal(outcome.status,"fulfilled");
  }
});

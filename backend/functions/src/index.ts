import { startDeliveryCommand, claimDeliveryRewardCommand, readMyDeliveries } from "./deliveries";
import { initializeApp } from "firebase-admin/app";
import { HttpsError, onCall } from "firebase-functions/v2/https";
import { assertLocalEnvironment, LOCAL_PROJECT } from "./localEnvironment";
import { createLocalFirestore, initializeIdentity } from "./identity";
import { acceptContractCommand, readMyContracts } from "./contracts";
import { readMyFleetState, readMyWarehouseState } from "./logisticsReads";
import { readMyEconomyState } from "./economy";
import { createCompanyCommand, readMyCompanyState } from "./company";

// Fail before exporting a function or initializing any SDK outside the local environment.
assertLocalEnvironment(process.env, true);

// Explicit emulator-only credential: never discover ADC or a developer's cloud credentials.
initializeApp({
  projectId: LOCAL_PROJECT,
  credential: {
    getAccessToken: async () => ({ access_token: "owner", expires_in: 3600 }),
  },
});

const localFirestore = createLocalFirestore();
export const ensurePlayerIdentity = onCall({ region: "us-central1", maxInstances: 1 }, (request) =>
  initializeIdentity(localFirestore, request.auth, request.data));

export const createCompany = onCall({ region: "us-central1", maxInstances: 1 }, (request) =>
  createCompanyCommand(localFirestore, request.auth, request.data));

export const getMyCompanyState = onCall({ region: "us-central1", maxInstances: 1 }, (request) =>
  readMyCompanyState(localFirestore, request.auth, request.data));

export const localHealth = onCall({ region: "us-central1", maxInstances: 1 }, (request) => {
  assertLocalEnvironment(process.env, true);
  // Emulator token decoding can produce an auth object without a UID for malformed tokens.
  if (typeof request.auth?.uid !== "string" || request.auth.uid.length === 0) {
    throw new HttpsError("unauthenticated", "Authentication required.", {
      code: "UNAUTHENTICATED", messageKey: "errors.unauthenticated",
    });
  }
  const data: unknown = request.data;
  if (typeof data !== "object" || data === null || Array.isArray(data) ||
      Object.keys(data).length !== 1 || !("protocolVersion" in data) || data.protocolVersion !== 1) {
    throw new HttpsError("invalid-argument", "Expected protocolVersion 1 only.", {
      code: "INVALID_ARGUMENT", messageKey: "errors.invalid_argument",
    });
  }
  // No database access, gameplay writes, identity echo, or idempotency record is needed.
  return { protocolVersion: 1, serverTime: new Date().toISOString(), result: { status: "ok" } };
});

export const getMyEconomyState = onCall({ region: "us-central1", maxInstances: 1 }, (request) =>
  readMyEconomyState(localFirestore, request.auth, request.data));

export const getMyFleetState = onCall({ region: "us-central1", maxInstances: 1 }, request =>
  readMyFleetState(localFirestore, request.auth, request.data));
export const getMyWarehouseState = onCall({ region: "us-central1", maxInstances: 1 }, request =>
  readMyWarehouseState(localFirestore, request.auth, request.data));

export const acceptContract = onCall({ region: "us-central1", maxInstances: 1 }, request =>
  acceptContractCommand(localFirestore, request.auth, request.data));
export const getMyContracts = onCall({ region: "us-central1", maxInstances: 1 }, request =>
  readMyContracts(localFirestore, request.auth, request.data));

export const startDelivery = onCall({ region: "us-central1", maxInstances: 1 }, request =>
  startDeliveryCommand(localFirestore, request.auth, request.data));
export const claimDeliveryReward = onCall({ region: "us-central1", maxInstances: 1 }, request =>
  claimDeliveryRewardCommand(localFirestore, request.auth, request.data));
export const getMyDeliveries = onCall({ region: "us-central1", maxInstances: 1 }, request =>
  readMyDeliveries(localFirestore, request.auth, request.data));

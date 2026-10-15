import { initializeApp } from "firebase-admin/app";
import { HttpsError, onCall } from "firebase-functions/v2/https";
import { assertLocalEnvironment, LOCAL_PROJECT } from "./localEnvironment";

// Fail before exporting a function or initializing any SDK outside the local environment.
assertLocalEnvironment(process.env, true);

// Explicit emulator-only credential: never discover ADC or a developer's cloud credentials.
initializeApp({
  projectId: LOCAL_PROJECT,
  credential: {
    getAccessToken: async () => ({ access_token: "owner", expires_in: 3600 }),
  },
});

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

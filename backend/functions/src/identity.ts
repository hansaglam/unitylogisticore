import { FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { HttpsError } from "firebase-functions/v2/https";
import { assertLocalEnvironment, LOCAL_PROJECT, LOCAL_ENDPOINTS } from "./localEnvironment";

/** Admin's Firestore constructor supports emulator transport without ADC/app credentials. */
export function createLocalFirestore(): Firestore {
  assertLocalEnvironment(process.env, true);
  // The installed Firestore SDK otherwise enables its trace utility by default.
  // No telemetry consumers or tracing are part of this local foundation.
  process.env.FIRESTORE_ENABLE_TRACING = "off";
  return new Firestore({ projectId: LOCAL_PROJECT, host: LOCAL_ENDPOINTS.FIRESTORE_EMULATOR_HOST, ssl: false });
}

type TrustedAuth = { uid: string } | undefined;

function validDocumentId(value: unknown): value is string {
  return typeof value === "string" && value.trim().length > 0 &&
    Buffer.byteLength(value, "utf8") <= 1500 && !value.includes("/") &&
    value !== "." && value !== ".." && !/^__.*__$/.test(value);
}

/** Call only with callable request.auth, never with request.data. */
export function requireUid(auth: TrustedAuth): string {
  if (!validDocumentId(auth?.uid) || auth.uid.length > 128) {
    throw new HttpsError("unauthenticated", "Authentication required.", {
      code: "UNAUTHENTICATED", messageKey: "errors.unauthenticated",
    });
  }
  return auth.uid;
}

export function validateIdentityRequest(data: unknown): void {
  if (typeof data !== "object" || data === null || Array.isArray(data) ||
      Object.keys(data).length !== 1 || !("protocolVersion" in data) || data.protocolVersion !== 1) {
    throw new HttpsError("invalid-argument", "Expected protocolVersion 1 only.", {
      code: "INVALID_ARGUMENT", messageKey: "errors.invalid_argument",
    });
  }
}

/** Local candidate schema only: never a production migration/bootstrap endpoint. */
export async function initializeIdentity(db: Firestore, auth: TrustedAuth, data: unknown) {
  assertLocalEnvironment(process.env, true);
  const uid = requireUid(auth);
  validateIdentityRequest(data);
  const ref = db.collection("v2Players").doc(uid);
  await db.runTransaction(async transaction => {
    const snapshot = await transaction.get(ref);
    if (snapshot.exists) {
      const existing = snapshot.data()!;
      if (existing.schemaVersion !== 1 || existing.authUid !== uid ||
          existing.identityStatus !== "active" || !(existing.createdAt instanceof Timestamp)) {
        throw new HttpsError("failed-precondition", "Identity requires review.", {
          code: "IDENTITY_CONFLICT", messageKey: "errors.identity_conflict",
        });
      }
      return;
    }
    transaction.create(ref, {
      schemaVersion: 1, authUid: uid, createdAt: FieldValue.serverTimestamp(), identityStatus: "active",
    });
  });
  return {
    protocolVersion: 1, serverTime: new Date().toISOString(),
    result: { playerId: uid, schemaVersion: 1, identityStatus: "active" },
  };
}

/** No callable export. For future mutations pass their transaction to avoid a check/write race.
 * The caller must pass verified request.auth, not a payload object. No ownership writes here.
 */
export async function requireCompanyOwner(
  db: Firestore, auth: TrustedAuth, companyId: unknown, transaction?: Transaction,
): Promise<void> {
  assertLocalEnvironment(process.env, true);
  const uid = requireUid(auth);
  if (!validDocumentId(companyId)) {
    throw new HttpsError("invalid-argument", "Invalid company identifier.", {
      code: "INVALID_ARGUMENT", messageKey: "errors.invalid_argument",
    });
  }
  const ref = db.collection("v2Companies").doc(companyId);
  const snapshot = await (transaction ? transaction.get(ref) : ref.get());
  if (!snapshot.exists) {
    throw new HttpsError("not-found", "Company unavailable.", {
      code: "COMPANY_NOT_FOUND", messageKey: "errors.company_not_found",
    });
  }
  const owner = snapshot.data()?.ownerUid;
  if (!validDocumentId(owner) || owner.length > 128) {
    throw new HttpsError("failed-precondition", "Company requires review.", {
      code: "COMPANY_INVALID", messageKey: "errors.company_invalid",
    });
  }
  if (owner !== uid) {
    throw new HttpsError("permission-denied", "Company access denied.", {
      code: "COMPANY_ACCESS_DENIED", messageKey: "errors.company_access_denied",
    });
  }
}

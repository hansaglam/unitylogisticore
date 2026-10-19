import { createHash } from "node:crypto";
import { FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { HttpsError, FunctionsErrorCode } from "firebase-functions/v2/https";
import { requireUid } from "./identity";
import { assertLocalEnvironment } from "./localEnvironment";

const errors = {
  UNAUTHENTICATED: "unauthenticated", INVALID_ARGUMENT: "invalid-argument",
  UNSUPPORTED_PROTOCOL: "failed-precondition", IDEMPOTENCY_CONFLICT: "already-exists",
  PERMISSION_DENIED: "permission-denied", FAILED_PRECONDITION: "failed-precondition",
  RETRY_LATER: "unavailable", INTERNAL: "internal",
  COMPANY_EXISTS: "already-exists", COMPANY_STATE_INVALID: "failed-precondition",
  MIGRATION_REQUIRED: "failed-precondition",
  OFFER_EXPIRED: "failed-precondition", CONTRACT_NOT_OFFERED: "failed-precondition",
  CONTRACT_INELIGIBLE: "failed-precondition", CONTRACT_QUOTA_REACHED: "resource-exhausted",
} as const satisfies Record<string, FunctionsErrorCode>;
export type CommandErrorCode = keyof typeof errors;

/** Only fixed public codes/keys; never forward database errors or arbitrary domain text. */
export class CommandError extends HttpsError {
  constructor(code: CommandErrorCode) {
    super(errors[code], code, { code, messageKey: `errors.${code.toLowerCase()}` });
  }
}

export type Json = null | boolean | number | string | Json[] | { [key: string]: Json };
export const MAX_COMMAND_BYTES = 8192;
export const MAX_RESULT_BYTES = 8192;

/** Canonical JSON: sorted object keys, ordered arrays, finite numbers, bounded depth/size.
 * No Dates, undefined, class instances, prototype keys, getters or cyclic structures.
 */
export function canonicalJson(value: unknown, limit = MAX_COMMAND_BYTES): string {
  let nodes = 0;
  function normalize(input: unknown, depth: number): Json {
    if (++nodes > 2048 || depth > 12) throw new CommandError("INVALID_ARGUMENT");
    if (input === null || typeof input === "boolean") return input;
    if (typeof input === "string") {
      if (Buffer.byteLength(input, "utf8") > limit) throw new CommandError("INVALID_ARGUMENT");
      return input;
    }
    if (typeof input === "number" && Number.isFinite(input)) return Object.is(input, -0) ? 0 : input;
    if (Array.isArray(input)) {
      if (input.length > 2048) throw new CommandError("INVALID_ARGUMENT");
      return Array.from(input, item => normalize(item, depth + 1));
    }
    if (typeof input !== "object" || input === null || Object.getPrototypeOf(input) !== Object.prototype) {
      throw new CommandError("INVALID_ARGUMENT");
    }
    const out: { [key: string]: Json } = {};
    for (const key of Object.keys(input).sort()) {
      const descriptor = Object.getOwnPropertyDescriptor(input, key)!;
      if (["__proto__", "constructor", "prototype"].includes(key) || !('value' in descriptor)) {
        throw new CommandError("INVALID_ARGUMENT");
      }
      out[key] = normalize(descriptor.value, depth + 1);
    }
    return out;
  }
  const encoded = JSON.stringify(normalize(value, 0));
  if (Buffer.byteLength(encoded, "utf8") > limit) throw new CommandError("INVALID_ARGUMENT");
  return encoded;
}

export function parseCommandRequest(data: unknown): { idempotencyKey: string; payload: unknown } {
  canonicalJson(data);
  if (typeof data !== "object" || data === null || Array.isArray(data) ||
      Object.keys(data).sort().join(",") !== "idempotencyKey,payload,protocolVersion") {
    throw new CommandError("INVALID_ARGUMENT");
  }
  const request = data as Record<string, unknown>;
  if (request.protocolVersion !== 1) throw new CommandError("UNSUPPORTED_PROTOCOL");
  if (typeof request.idempotencyKey !== "string" || !/^[A-Za-z0-9_-]{1,128}$/.test(request.idempotencyKey) ||
      /^__.*__$/.test(request.idempotencyKey)) {
    throw new CommandError("INVALID_ARGUMENT");
  }
  if (typeof request.payload !== "object" || request.payload === null || Array.isArray(request.payload)) {
    throw new CommandError("INVALID_ARGUMENT");
  }
  return { idempotencyKey: request.idempotencyKey, payload: request.payload };
}

export function requestFingerprint(command: string, companyId: string | null, payload: Json): string {
  return createHash("sha256").update(canonicalJson({ command, protocolVersion: 1, companyId, payload }, 16384)).digest("hex");
}

export interface CommandDefinition<P extends Json> {
  /** Server-selected identity; never taken from request.data. Keep semantics stable for stored receipts. */
  name: string;
  validate(payload: unknown): P;
  /** Optional canonical scope resolution. Read-only, transaction-bound, no external side effects. */
  resolveCompany?(transaction: Transaction, uid: string, payload: P): Promise<string>;
  /** Reads/ownership/preconditions first, then writes. May run repeatedly. No network side effects.
   * Must not write/delete receipts or modify another UID's state. Result must be safe public JSON.
   */
  apply(transaction: Transaction, uid: string, payload: P, companyId: string | null): Promise<Json>;
}

function responseFromReceipt(data: FirebaseFirestore.DocumentData | undefined, hash: string, command: string, scope: string | null) {
  if (!data || data.receiptSchemaVersion !== 1 || data.protocolVersion !== 1 ||
      typeof data.requestFingerprint !== "string" || !/^[a-f0-9]{64}$/.test(data.requestFingerprint) ||
      typeof data.command !== "string" || !(data.committedAt instanceof Timestamp)) {
    throw new CommandError("FAILED_PRECONDITION");
  }
  if (data.requestFingerprint !== hash || data.command !== command || data.companyId !== scope) {
    throw new CommandError("IDEMPOTENCY_CONFLICT");
  }
  let result: Json;
  try { result = JSON.parse(canonicalJson(data.result, MAX_RESULT_BYTES)) as Json; }
  catch { throw new CommandError("FAILED_PRECONDITION"); }
  return { protocolVersion: 1, serverTime: data.committedAt.toDate().toISOString(), result };
}

/** Internal only. No generic callable or production bootstrap is exported. */
export async function executeCommand<P extends Json>(
  db: Firestore, auth: { uid: string } | undefined, data: unknown, definition: CommandDefinition<P>,
) {
  assertLocalEnvironment(process.env, true);
  let uid: string;
  try { uid = requireUid(auth); } catch { throw new CommandError("UNAUTHENTICATED"); }
  const request = parseCommandRequest(data);
  try {
    if (!/^[A-Za-z][A-Za-z0-9]{0,63}$/.test(definition.name)) throw new CommandError("INTERNAL");
    const payload = JSON.parse(canonicalJson(definition.validate(request.payload))) as P;
    const ref = db.collection("v2Players").doc(uid).collection("commands").doc(request.idempotencyKey);
    const execution = await db.runTransaction(async transaction => {
      const existing = await transaction.get(ref);
      const scope = definition.resolveCompany ? await definition.resolveCompany(transaction, uid, payload) : null;
      if (scope !== null && (typeof scope !== "string" || !/^[A-Za-z0-9_-]{1,128}$/.test(scope))) {
        throw new CommandError("FAILED_PRECONDITION");
      }
      const hash = requestFingerprint(definition.name, scope, payload);
      if (existing.exists) return { response: responseFromReceipt(existing.data(), hash, definition.name, scope), hash, scope };
      const result = await definition.apply(transaction, uid, payload, scope);
      let bounded: Json;
      try { bounded = JSON.parse(canonicalJson(result, MAX_RESULT_BYTES)) as Json; }
      catch { throw new CommandError("INTERNAL"); }
      transaction.create(ref, {
        receiptSchemaVersion: 1, command: definition.name, protocolVersion: 1,
        requestFingerprint: hash, companyId: scope, result: bounded, committedAt: FieldValue.serverTimestamp(),
      });
      return { response: null, hash, scope };
    });
    // The server timestamp is resolved only after commit. Failure here is safe to retry with the same key.
    if (execution.response) return execution.response;
    return responseFromReceipt((await ref.get()).data(), execution.hash, definition.name, execution.scope);
  } catch (error) {
    if (error instanceof CommandError) throw error;
    // Existing ownership primitives use native callable errors. Normalize without leaking details.
    if (error instanceof HttpsError) {
      if (error.code === "permission-denied") throw new CommandError("PERMISSION_DENIED");
      if (error.code === "not-found" || error.code === "failed-precondition") throw new CommandError("FAILED_PRECONDITION");
    }
    // Only recognized transient transport codes are retryable; unexpected failures stay INTERNAL.
    const code = typeof error === "object" && error !== null && "code" in error ? error.code : undefined;
    if ([4, 10, 14, "deadline-exceeded", "aborted", "unavailable"].includes(code as number | string)) {
      throw new CommandError("RETRY_LATER");
    }
    throw new CommandError("INTERNAL");
  }
}

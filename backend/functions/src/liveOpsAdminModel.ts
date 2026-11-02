import { canonicalJson, CommandError } from "./commands";
import { safe, versionId } from "./economyModel";
import { digest } from "./logisticsModel";

// Policy only: a future adapter must authenticate and load these contexts itself.
// No player auth, environment reads, clock, SDK client, receipt or catalog writes.
export const ADMIN_STATES = Object.freeze(["DRAFT", "PENDING_APPROVAL", "APPROVED", "PUBLISHED", "REJECTED", "CANCELLED"] as const);
export type AdminState = typeof ADMIN_STATES[number];
export type AdminCapability = "EDITOR" | "RELEASER";
const ACTIONS = ["EDIT", "SUBMIT", "APPROVE", "REJECT", "CANCEL", "PUBLISH", "ACTIVATE", "ROLLBACK"] as const;
const MAX_TIME = 253402300799999;
const DOMAIN = "logisticore_liveops_admin_v1";
function fail(): never { throw new CommandError("FAILED_PRECONDITION"); }
function deny(): never { throw new CommandError("PERMISSION_DENIED"); }

/** Reject hidden/accessor/symbol properties before reusing the existing canonical format. */
function normalized(value: unknown): unknown {
  let nodes = 0;
  function shape(v: unknown, depth: number): void {
    if (++nodes > 2048 || depth > 12) fail();
    if (v === null || typeof v !== "object") return;
    const array = Array.isArray(v);
    if (Object.getPrototypeOf(v) !== (array ? Array.prototype : Object.prototype)) fail();
    const keys = Reflect.ownKeys(v);
    if (keys.length > 2049) fail();
    if (array && (v.length > 2048 || keys.length !== v.length + 1)) fail();
    for (const key of keys) {
      if (typeof key !== "string") fail();
      if (array && key === "length") continue;
      const d = Object.getOwnPropertyDescriptor(v, key)!;
      if (!d.enumerable || !("value" in d) ||
          (array && (!/^(0|[1-9][0-9]*)$/.test(key) || Number(key) >= v.length))) fail();
      shape(d.value, depth + 1);
    }
  }
  shape(value, 0);
  return JSON.parse(canonicalJson(value, 32768)) as unknown;
}
function object(raw: unknown, fields: string): Record<string, unknown> {
  if (raw === null || typeof raw !== "object" || Array.isArray(raw) ||
      Object.keys(raw).sort().join(",") !== fields.split(",").sort().join(",")) fail();
  return raw as Record<string, unknown>;
}
function hash(raw: unknown): string {
  if (typeof raw !== "string" || !/^[a-f0-9]{64}$/.test(raw)) fail();
  return raw;
}
function time(raw: unknown): number { return safe(raw, MAX_TIME); }
function state(raw: unknown): AdminState {
  if (!ADMIN_STATES.includes(raw as AdminState)) fail();
  return raw as AdminState;
}
function environment(raw: unknown) {
  const v = object(raw, "projectId,namespace");
  if (v.projectId !== "demo-logisticore-local") fail();
  // Namespaces separate synthetic test contexts, never select a cloud project.
  return { projectId: "demo-logisticore-local" as const, namespace: versionId(v.namespace) };
}
function pin(raw: unknown) {
  const v = object(raw, "version,hash");
  return { version: versionId(v.version), hash: hash(v.hash) };
}
function pins(raw: unknown) {
  const v = object(raw, "economy,logistics,cost,market");
  return { economy: pin(v.economy), logistics: pin(v.logistics), cost: pin(v.cost), market: pin(v.market) };
}
function target(raw: unknown) {
  const v = object(raw, "contractSchemaVersion,liveOpsVersion,contractVersion");
  if (v.contractSchemaVersion !== 5) fail();
  return { contractSchemaVersion: 5 as const, liveOpsVersion: versionId(v.liveOpsVersion), contractVersion: versionId(v.contractVersion) };
}
function evidence(raw: unknown) {
  const v = object(raw, "proposalId,revision,contentHash,pinsHash,validationHash,previewHash,validatedAt");
  return { proposalId: versionId(v.proposalId), revision: safe(v.revision), contentHash: hash(v.contentHash),
    pinsHash: hash(v.pinsHash), validationHash: hash(v.validationHash), previewHash: hash(v.previewHash), validatedAt: time(v.validatedAt) };
}
function approval(raw: unknown) {
  const v = object(raw, "proposalId,revision,contentHash,validationHash,previewHash,pins,target,action,actorRef,approverRef,approverGrantRevision,environment,approvedAt,notBefore,expiresAt");
  if (v.action !== "PUBLISH") fail(); // Activation/rollback approvals belong to later phases.
  const a = { proposalId: versionId(v.proposalId), revision: safe(v.revision), contentHash: hash(v.contentHash),
    validationHash: hash(v.validationHash), previewHash: hash(v.previewHash), pins: pins(v.pins), target: target(v.target),
    action: "PUBLISH" as const, actorRef: versionId(v.actorRef), approverRef: versionId(v.approverRef),
    approverGrantRevision: safe(v.approverGrantRevision), environment: environment(v.environment),
    approvedAt: time(v.approvedAt), notBefore: time(v.notBefore), expiresAt: time(v.expiresAt) };
  if (a.actorRef === a.approverRef || a.approvedAt > a.notBefore || a.notBefore >= a.expiresAt) fail();
  return a;
}
export type AdminApproval = ReturnType<typeof approval>;
export type ValidationEvidence = ReturnType<typeof evidence>;

export function validateAdminContext(raw: unknown) {
  const v = object(normalized(raw), "principalRef,enabled,capabilities,environment,grantRevision,validFrom,validUntil");
  if (typeof v.enabled !== "boolean" || !Array.isArray(v.capabilities) || v.capabilities.length > 2 ||
      v.capabilities.some(c => c !== "EDITOR" && c !== "RELEASER") || new Set(v.capabilities).size !== v.capabilities.length) fail();
  const result = { principalRef: versionId(v.principalRef), enabled: v.enabled,
    capabilities: v.capabilities as AdminCapability[], environment: environment(v.environment), grantRevision: safe(v.grantRevision),
    validFrom: time(v.validFrom), validUntil: time(v.validUntil) };
  if (result.validFrom >= result.validUntil) fail();
  return result;
}
export type AdminContext = ReturnType<typeof validateAdminContext>;
export function requireAdminCapability(raw: unknown, capability: AdminCapability, scope: unknown, now: number): AdminContext {
  const c = validateAdminContext(raw), e = environment(normalized(scope)); time(now);
  if (!["EDITOR", "RELEASER"].includes(capability) || !c.enabled || !c.capabilities.includes(capability) ||
      digest(c.environment) !== digest(e) || now < c.validFrom || now > c.validUntil) deny();
  return c;
}

/** Hash commitment only; financial catalog validity must come from existing validators. */
export function adminContentHash(content: unknown): string { return digest(normalized(content)); }

function evidenceMatches(p: { proposalId: string; revision: number; contentHash: string; pins: ReturnType<typeof pins> }, e: ValidationEvidence): void {
  if (e.proposalId !== p.proposalId || e.revision !== p.revision || e.contentHash !== p.contentHash || e.pinsHash !== digest(p.pins)) fail();
}
function approvalMatches(p: { proposalId: string; revision: number; contentHash: string; authorRef: string;
  environment: ReturnType<typeof environment>; pins: ReturnType<typeof pins>; target: ReturnType<typeof target>; evidence: ValidationEvidence | null }, a: AdminApproval): void {
  if (!p.evidence) fail();
  evidenceMatches(p, p.evidence);
  if (a.proposalId !== p.proposalId || a.revision !== p.revision || a.contentHash !== p.contentHash ||
      a.actorRef !== p.authorRef || digest(a.environment) !== digest(p.environment) || digest(a.pins) !== digest(p.pins) ||
      digest(a.target) !== digest(p.target) || a.validationHash !== p.evidence.validationHash || a.previewHash !== p.evidence.previewHash ||
      a.approvedAt < p.evidence.validatedAt) fail();
}
export function validateAdminProposal(raw: unknown) {
  const v = object(normalized(raw), "schemaVersion,proposalId,revision,state,authorRef,environment,contentHash,pins,target,evidence,approval");
  if (v.schemaVersion !== 1) fail();
  const p = { schemaVersion: 1 as const, proposalId: versionId(v.proposalId), revision: safe(v.revision), state: state(v.state),
    authorRef: versionId(v.authorRef), environment: environment(v.environment), contentHash: hash(v.contentHash), pins: pins(v.pins), target: target(v.target),
    evidence: v.evidence === null ? null : evidence(v.evidence), approval: v.approval === null ? null : approval(v.approval) };
  if (p.evidence) evidenceMatches(p, p.evidence);
  if (p.approval) approvalMatches(p, p.approval);
  if (["PENDING_APPROVAL", "APPROVED", "PUBLISHED", "REJECTED"].includes(p.state) && !p.evidence) fail();
  if (["APPROVED", "PUBLISHED"].includes(p.state) && !p.approval) fail();
  if (["DRAFT", "PENDING_APPROVAL", "REJECTED"].includes(p.state) && p.approval) fail();
  return p;
}
export type AdminProposal = ReturnType<typeof validateAdminProposal>;

export function validatePublicationApproval(raw: unknown, proposal: unknown, trustedApprover: unknown, now: number, intendedAction: string = "PUBLISH"): AdminApproval {
  const p = validateAdminProposal(proposal), a = approval(normalized(raw));
  if (intendedAction !== "PUBLISH") fail();
  approvalMatches(p, a);
  const c = requireAdminCapability(trustedApprover, "RELEASER", p.environment, now);
  if (c.principalRef !== a.approverRef || c.grantRevision !== a.approverGrantRevision ||
      a.approvedAt < c.validFrom || a.expiresAt > c.validUntil || now < a.notBefore || now > a.expiresAt) fail();
  return a;
}

/** Revisions identify content; lifecycle state is an additional expected-state CAS token. */
export function planDraftEdit(raw: unknown, request: unknown, trustedEditor: unknown, now: number): AdminProposal {
  const p = validateAdminProposal(raw);
  const r = object(normalized(request), "expectedRevision,contentHash,pins,target");
  const c = requireAdminCapability(trustedEditor, "EDITOR", p.environment, now);
  if (c.principalRef !== p.authorRef) deny();
  if (p.state !== "DRAFT" || safe(r.expectedRevision) !== p.revision) fail();
  return validateAdminProposal({ ...p, revision: safe(p.revision + 1), contentHash: hash(r.contentHash),
    pins: pins(r.pins), target: target(r.target), evidence: null, approval: null });
}

const TRANSITIONS: Record<AdminState, readonly AdminState[]> = {
  DRAFT: ["PENDING_APPROVAL", "CANCELLED"], PENDING_APPROVAL: ["APPROVED", "REJECTED", "CANCELLED"],
  APPROVED: ["PUBLISHED", "CANCELLED"], PUBLISHED: [], REJECTED: [], CANCELLED: [],
};
export function planProposalTransition(raw: unknown, request: unknown, trustedActor: unknown, now: number, trustedApprover: unknown = null): AdminProposal {
  const p = validateAdminProposal(raw);
  const r = object(normalized(request), "expectedRevision,expectedState,to,evidence,approval");
  const to = state(r.to);
  if (safe(r.expectedRevision) !== p.revision || state(r.expectedState) !== p.state || !TRANSITIONS[p.state].includes(to)) fail();
  const actor = validateAdminContext(trustedActor);
  if (to === "PENDING_APPROVAL" || (to === "CANCELLED" && actor.principalRef === p.authorRef && actor.capabilities.includes("EDITOR"))) {
    requireAdminCapability(actor, "EDITOR", p.environment, now);
    if (actor.principalRef !== p.authorRef) deny();
  } else requireAdminCapability(actor, "RELEASER", p.environment, now);
  let nextEvidence = p.evidence, nextApproval = p.approval;
  if (to === "PENDING_APPROVAL") {
    nextEvidence = evidence(r.evidence); evidenceMatches(p, nextEvidence);
    if (nextEvidence.validatedAt > now) fail();
  } else if (r.evidence !== null) fail();
  if (to === "APPROVED") {
    nextApproval = validatePublicationApproval(r.approval, p, actor, now);
    if (nextApproval.approvedAt !== now) fail();
  } else if (r.approval !== null) fail();
  if (to === "PUBLISHED") validatePublicationApproval(p.approval, p, trustedApprover, now);
  return validateAdminProposal({ ...p, state: to, evidence: nextEvidence, approval: nextApproval });
}

/** Eligibility plan only. Existence/immutability of target documents needs a future transaction. */
export function planPublicationIntent(raw: unknown, expectedRevision: number, trustedActor: unknown, trustedApprover: unknown, now: number) {
  const p = validateAdminProposal(raw);
  const planned = planProposalTransition(p, { expectedRevision, expectedState: "APPROVED", to: "PUBLISHED", evidence: null, approval: null }, trustedActor, now, trustedApprover);
  return { proposalId: p.proposalId, revision: p.revision, contentHash: p.contentHash, pins: p.pins, target: p.target,
    approvalHash: digest(p.approval), plannedProposal: planned };
}

export function adminOperationIdentity(raw: unknown) {
  const r = object(normalized(raw), "domain,environment,action,actorRef,idempotencyKey,body");
  if (r.domain !== DOMAIN || !ACTIONS.includes(r.action as typeof ACTIONS[number]) ||
      typeof r.idempotencyKey !== "string" || !/^[A-Za-z0-9_-]{1,128}$/.test(r.idempotencyKey) || /^__.*__$/.test(r.idempotencyKey)) fail();
  if (r.body === null || typeof r.body !== "object" || Array.isArray(r.body)) fail();
  const scope = { domain: DOMAIN, environment: environment(r.environment), action: r.action as string,
    actorRef: versionId(r.actorRef), idempotencyKey: r.idempotencyKey };
  return { operationId: digest(scope), fingerprint: digest({ ...scope, body: r.body }) };
}
export function assertSameAdminOperation(recorded: unknown, requested: unknown): void {
  function identity(raw: unknown) {
    const r = object(normalized(raw), "operationId,fingerprint");
    return { operationId: hash(r.operationId), fingerprint: hash(r.fingerprint) };
  }
  const a = identity(recorded), b = identity(requested);
  if (a.operationId !== b.operationId || a.fingerprint !== b.fingerprint) throw new CommandError("IDEMPOTENCY_CONFLICT");
}

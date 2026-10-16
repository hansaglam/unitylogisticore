import { createHash } from "node:crypto";
import { DocumentData, FieldValue, Firestore, Transaction } from "firebase-admin/firestore";
import { CommandError, executeCommand } from "./commands";
import { readFoundation } from "./company";
import { requireUid, validateIdentityRequest } from "./identity";
import { assertLocalEnvironment } from "./localEnvironment";
import { checkedAdd, configHash, ECONOMY_READY, exact, invalid, levelFor, safe, validateConfig, versionId } from "./economyModel";

function operationId(companyId: string, source: string): string {
  return createHash("sha256").update(`${companyId}:${source}`).digest("hex");
}
/** Internal server orchestration only. Payload must be empty; selector is trusted backend input.
 * No callable exports this mechanism. Every source is permanently single-use across request keys.
 */
export function runEconomyCommand(db: Firestore, auth: { uid: string } | undefined, data: unknown,
  action: { kind: "initialize" | "entitlement"; id: string }) {
  const id = versionId(action.id);
  if (!["initialize", "entitlement"].includes(action.kind)) invalid();
  const kind = action.kind;
  const snapshots = new WeakMap<Transaction, Awaited<ReturnType<typeof readFoundation>>>();
  return executeCommand(db, auth, data, {
    name: kind === "initialize" ? "initializeEconomy" : "consumeEconomyEntitlement",
    validate(payload) {
      exact(payload as DocumentData, "");
      return { kind, id };
    },
    async resolveCompany(tx, uid) {
      const state = await readFoundation(tx, db, uid);
      if (!state.companyData) invalid();
      snapshots.set(tx, state);
      return state.companyId;
    },
    async apply(tx, uid) {
      const state = snapshots.get(tx)!;
      const source = kind === "initialize" ? "initialization" : `entitlement_${id}`;
      const claimRef = state.companyRef.collection("claims").doc(source);
      const opId = operationId(state.companyId, source);
      const ledgerRef = state.companyRef.collection("ledger").doc(opId);
      const [claim, ledger] = await tx.getAll(claimRef, ledgerRef);
      if (claim!.exists || ledger!.exists) invalid();
      let config;
      let delta: number;
      let xpDelta: number;
      let reason: string;
      if (kind === "initialize") {
        if (state.economy) invalid();
        config = validateConfig((await tx.get(db.collection("v2WorldConfig").doc(id))).data(), id);
        delta = config.startingBalance; xpDelta = config.initialXp; reason = "initialization";
      } else {
        if (!state.economy) invalid();
        config = state.economy.config;
        const entitlement = (await tx.get(state.companyRef.collection("entitlements").doc(id))).data();
        exact(entitlement, "schemaVersion,approval,ownerUid,companyId,configVersion,configHash,currency,scale,delta,xpDelta,reason");
        if (entitlement.schemaVersion !== 1 || entitlement.approval !== "synthetic_emulator_only" || entitlement.ownerUid !== uid ||
            entitlement.companyId !== state.companyId || entitlement.configVersion !== config.version || entitlement.configHash !== configHash(config) ||
            entitlement.currency !== config.currency || entitlement.scale !== config.scale || !Number.isSafeInteger(entitlement.delta)) invalid();
        safe(Math.abs(entitlement.delta), config.maxBalance); safe(entitlement.xpDelta, config.maxXp);
        if (!["synthetic_debit", "synthetic_credit", "synthetic_reward"].includes(entitlement.reason) ||
            (entitlement.reason === "synthetic_debit" ? entitlement.delta >= 0 || entitlement.xpDelta !== 0 : entitlement.delta < 0)) invalid();
        delta = entitlement.delta; xpDelta = entitlement.xpDelta; reason = entitlement.reason;
      }
      const balance = checkedAdd(state.economy?.balance ?? 0, delta, config.maxBalance);
      const xp = checkedAdd(state.economy?.xp ?? 0, xpDelta, config.maxXp);
      const level = levelFor(xp, config);
      const timestamp = FieldValue.serverTimestamp();
      const common = { schemaVersion: 1, configVersion: config.version, configHash: configHash(config), updatedAt: timestamp };
      const finance = { ...common, balance, currency: config.currency, scale: config.scale };
      const progression = { ...common, xp, level, rulesVersion: config.rulesVersion };
      if (kind === "initialize") {
        tx.create(state.financeRef, finance); tx.create(state.progressionRef, progression);
        tx.update(state.companyRef, { bootstrapStatus: ECONOMY_READY });
      } else {
        tx.update(state.financeRef, finance); tx.update(state.progressionRef, progression);
      }
      tx.create(claimRef, { schemaVersion: 1, source, operationId: opId, configVersion: config.version, committedAt: timestamp });
      // A zero delta is retained for initialization/XP-only evidence, never a second wallet.
      tx.create(ledgerRef, { schemaVersion: 1, operationId: opId, reason, delta, balanceAfter: balance,
        currency: config.currency, scale: config.scale, source, configVersion: config.version, rulesVersion: config.rulesVersion, committedAt: timestamp });
      return { companyId: state.companyId, economyReady: true, operationId: opId, balance: String(balance), xp, level,
        currency: config.currency, scale: config.scale, configVersion: config.version, rulesVersion: config.rulesVersion };
    },
  });
}

export async function readMyEconomyState(db: Firestore, auth: { uid: string } | undefined, data: unknown) {
  assertLocalEnvironment(process.env, true);
  const uid = requireUid(auth); validateIdentityRequest(data);
  try {
    const state = await db.runTransaction(tx => readFoundation(tx, db, uid), { readOnly: true });
    const economy = state.economy;
    return { protocolVersion: 1, serverTime: new Date().toISOString(), result: economy ? {
      hasCompany: true, economyReady: true, companyId: state.companyId, balance: String(economy.balance),
      currency: economy.config.currency, scale: economy.config.scale, xp: economy.xp, level: economy.level,
      configVersion: economy.config.version, rulesVersion: economy.config.rulesVersion,
    } : { hasCompany: !!state.companyData, economyReady: false } };
  } catch (error) {
    if (error instanceof CommandError) throw error;
    throw new CommandError("INTERNAL");
  }
}

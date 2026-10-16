import { createHash } from "node:crypto";
import { DocumentData, Timestamp } from "firebase-admin/firestore";
import { canonicalJson, CommandError } from "./commands";

export const ECONOMY_READY = "economy_ready";
export function invalid(): never { throw new CommandError("FAILED_PRECONDITION"); }
export function exact(data: DocumentData | undefined, keys: string): asserts data is DocumentData {
  if (!data || Object.keys(data).sort().join(",") !== keys.split(",").sort().join(",")) invalid();
}
export function safe(value: unknown, max = Number.MAX_SAFE_INTEGER): number {
  if (typeof value !== "number" || !Number.isSafeInteger(value) || value < 0 || value > max) invalid();
  return value;
}
export function checkedAdd(value: number, delta: number, max: number): number {
  safe(value, max);
  if (!Number.isSafeInteger(delta)) invalid();
  return safe(value + delta, max);
}
export function versionId(value: unknown): string {
  if (typeof value !== "string" || !/^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$/.test(value)) invalid();
  return value;
}
export function validateConfig(data: DocumentData | undefined, version: string) {
  exact(data, "schemaVersion,version,approval,currency,scale,maxBalance,startingBalance,maxXp,initialXp,thresholds,rulesVersion");
  if (data.schemaVersion !== 1 || data.version !== version || data.approval !== "synthetic_emulator_only" ||
      typeof data.currency !== "string" || !/^[A-Z]{3}$/.test(data.currency)) invalid();
  versionId(version); versionId(data.rulesVersion);
  safe(data.scale, 6); safe(data.maxBalance); safe(data.startingBalance, data.maxBalance);
  safe(data.maxXp); safe(data.initialXp, data.maxXp);
  if (!Array.isArray(data.thresholds) || data.thresholds.length < 1 || data.thresholds.length > 1000 || data.thresholds[0] !== 0) invalid();
  data.thresholds.forEach((n: unknown, i: number) => {
    safe(n, data.maxXp); if (i > 0 && (n as number) <= data.thresholds[i - 1]) invalid();
  });
  return { schemaVersion: 1, version, approval: "synthetic_emulator_only", currency: data.currency as string,
    scale: data.scale as number, maxBalance: data.maxBalance as number, startingBalance: data.startingBalance as number,
    maxXp: data.maxXp as number, initialXp: data.initialXp as number, thresholds: data.thresholds as number[], rulesVersion: data.rulesVersion as string };
}
export type EconomyConfig = ReturnType<typeof validateConfig>;
export function configHash(config: EconomyConfig): string { return createHash("sha256").update(canonicalJson(config, 32768)).digest("hex"); }
export function levelFor(xp: number, config: EconomyConfig): number {
  safe(xp, config.maxXp);
  return config.thresholds.filter(n => xp >= n).length;
}
export function validateEconomy(finance: DocumentData | undefined, progression: DocumentData | undefined, config: EconomyConfig) {
  exact(finance, "schemaVersion,balance,currency,scale,configVersion,configHash,updatedAt");
  exact(progression, "schemaVersion,xp,level,rulesVersion,configVersion,configHash,updatedAt");
  const hash = configHash(config);
  if (finance.schemaVersion !== 1 || progression.schemaVersion !== 1 || finance.currency !== config.currency || finance.scale !== config.scale ||
      finance.configVersion !== config.version || progression.configVersion !== config.version || finance.configHash !== hash || progression.configHash !== hash ||
      progression.rulesVersion !== config.rulesVersion || !(finance.updatedAt instanceof Timestamp) || !(progression.updatedAt instanceof Timestamp)) invalid();
  safe(finance.balance, config.maxBalance); safe(progression.xp, config.maxXp);
  if (progression.level !== levelFor(progression.xp, config)) invalid();
  return { balance: finance.balance as number, xp: progression.xp as number, level: progression.level as number, config };
}

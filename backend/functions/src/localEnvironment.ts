/** Local foundation only. This is not a production environment selector. */
export const LOCAL_PROJECT = "demo-logisticore-local";
export const LOCAL_ENDPOINTS = {
  FIREBASE_AUTH_EMULATOR_HOST: "127.0.0.1:9099",
  FIRESTORE_EMULATOR_HOST: "127.0.0.1:8080",
  FUNCTIONS_EMULATOR_HOST: "127.0.0.1:5001",
} as const;

type Environment = Readonly<Record<string, string | undefined>>;

/** Validate inherited configuration before the launcher adds its local defaults. */
export function assertSafeOverrides(env: Environment): void {
  for (const key of ["GCLOUD_PROJECT", "GOOGLE_CLOUD_PROJECT", "GCP_PROJECT", "FIREBASE_PROJECT_ID"]) {
    if (env[key] !== undefined && env[key] !== LOCAL_PROJECT) {
      throw new Error(`Local isolation: unexpected ${key}.`);
    }
  }
  for (const key of ["GOOGLE_APPLICATION_CREDENTIALS", "FIREBASE_TOKEN", "GOOGLE_OAUTH_ACCESS_TOKEN"]) {
    if (env[key]) throw new Error(`Local isolation: ${key} must be unset.`);
  }
  if (env.FIREBASE_CONFIG !== undefined) {
    let config: unknown;
    try { config = JSON.parse(env.FIREBASE_CONFIG); }
    catch { throw new Error("Local isolation: FIREBASE_CONFIG must be inline demo JSON."); }
    if (typeof config !== "object" || config === null || !("projectId" in config) ||
        config.projectId !== LOCAL_PROJECT) {
      throw new Error("Local isolation: unexpected FIREBASE_CONFIG project.");
    }
  }
  for (const [key, endpoint] of Object.entries(LOCAL_ENDPOINTS)) {
    if (env[key] !== undefined && env[key] !== endpoint) {
      throw new Error(`Local isolation: unexpected ${key}.`);
    }
  }
}

export function assertLocalEnvironment(env: Environment, functionsRuntime = false): void {
  assertSafeOverrides(env);
  if (env.GCLOUD_PROJECT !== LOCAL_PROJECT) throw new Error("Local isolation: demo project required.");
  for (const [key, endpoint] of Object.entries(LOCAL_ENDPOINTS)) {
    // The Functions discovery subprocess does not inherit arbitrary environment keys.
    // It needs Auth/Firestore endpoints, not an outbound Functions client endpoint.
    if (functionsRuntime && key === "FUNCTIONS_EMULATOR_HOST") continue;
    if (env[key] !== endpoint) throw new Error(`Local isolation: ${key} required.`);
  }
  if (functionsRuntime && env.FUNCTIONS_EMULATOR !== "true") {
    throw new Error("Local isolation: Functions emulator required.");
  }
}

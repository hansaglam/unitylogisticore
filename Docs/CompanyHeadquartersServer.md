# CITY-START-02 — Atomic company + headquarters registration (local Firebase emulator)

## Delivered in this Draft PR
- A **server-owned** set of 248 approved launch city IDs from the stacked Unity Türkiye–Avrupa map catalog, pinned as `lc_tur_europe_2026_11_v1`. Existing 81 province-center IDs, world-city IDs and country-scoped identities are preserved.
- A new authenticated, idempotent **`createCompanyWithHeadquarters`** callable (protocol v1, distinct from existing `createCompany`) accepting only `{name, headquarters:{countryCode, cityKey}}`. Server checks the exact approved country/city pair and rejects spoofed coordinates, currencies, owner IDs, balances and unsupported locations.
- A **single Firestore transaction** creates the **unchanged schemaVersion 1 company document**, writes `v2Companies/{companyId}/state/headquarters` as a separately validated immutable company-owned record, links `v2Players/{uid}.companyId`, and creates the command receipt via the existing idempotent command framework. All or none commit.
- New **`getMyCompanyHeadquarters`** read-only callable. For an existing company without this record it returns `{hasCompany:true,hasHeadquarters:false}` and writes nothing. A new company returns the validated country code, city key and catalog version.
- Existing `createCompany` and `getMyCompanyState` contracts are untouched. This preserves deployed account reads, economy, finance and legacy receipts. Old companies are not automatically moved or migrated.
- Unity `ClientCallableCatalog` registers the new command/read names and `ClientReadParser.TryCompanyHeadquarters` parses the optional authoritative read result. It **does not enable player-side command dispatch**. The UI remains an explicit **map preview** until the consent/confirmation + authenticated command workflow is approved.
- Deferred unit and emulator tests now cover dataset parity, malformed requests, strict Firestore document shape, legacy company compatibility, no-HQ reads, idempotent retries, no unauthorized direct database reads and account isolation.

## Critical environment limit
The current backend is **not production Firebase**. `backend/functions/src/localEnvironment.ts` rejects every non-`demo-logisticore-local` project and requires loopback Auth, Firestore and Functions emulators. This feature therefore provides **local emulator persistence only**, and makes **no changes to published players' live data**. Do **not** remove this guard or point the local test process at a production project.

Unity's `ClientCommandAuthorization.AllowsLiveFunctions` intentionally returns `false`, and `ClientCommandSession` currently only dispatches `acceptContract` (which is also held). This PR leaves those security boundaries unchanged. Neither preview button nor company scene can silently create a company.

## Server data contract
For new companies created with HQ:
- `v2Companies/{companyId}`: `schemaVersion:1, ownerUid, name, bootstrapStatus, createdAt` (exact existing fields).
- `v2Companies/{companyId}/state/headquarters`: `schemaVersion:1, companyId, ownerUid, countryCode, cityKey, catalogVersion, selectionKind:initial_company_setup, selectedAt`.
- `v2Players/{uid}`: only the pre-existing `companyId` linking field is added as the existing command does.
- `v2Players/{uid}/commands/{idempotencyKey}`: idempotent request receipt, as for existing company creation.

For existing companies no headquarters record is fabricated. Do not infer a central city from fleet, city artwork, or device locale.

## Next work (CITY-START-03)
1. Build a **safe confirmed onboarding UI** including company name, selected eligible city, and explicit user confirmation. First load trusted `getMyCompanyState`/`getMyCompanyHeadquarters` so existing users cannot overwrite their company.
2. Create separately approved, gated command transport for `createCompanyWithHeadquarters`, keeping the emulator security boundary and current key/receipt behavior. Establish a distinct, reviewed production rollout path (this repository does not currently contain one).
3. Make server-owned company HQ available to map default framing only when the trusted read succeeded. Existing accounts with no HQ keep their current map behavior.
4. Add balanced starting-fleet/depot and route eligibility based on actual supported, authorized road networks; HQ alone does not grant a depot or Europe delivery contracts.
5. At the agreed **final** verification milestone run TypeScript build/typecheck, backend unit tests, Firestore emulator tests, Unity compilation, EditMode/PlayMode and Android/iOS device checks; verify old data migrations before merging or releasing.

This is a stacked Draft PR; do not merge independently into `main`.

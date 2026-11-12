# Client backend contracts — CLIENT-API-01

UNITY-01A remains the Firebase connection gate. CLIENT-API-01 adds typed read parsing and a gated callable session. It does **not** authorize a live connection.

The committed environment asset is Unconfigured and empty. `LocalClientEnvironment` is gitignored. Production is always refused.

Verified public callables exist only on `demo-logisticore-local` / Functions emulator `us-central1`. Unity does not invoke them until a later connection approval.

## Read callables (documented + backend-implemented, Unity unwired)

`localHealth`, `getMyCompanyState`, `getMyEconomyState`, `getMyFleetState`, `getMyWarehouseState`, `getMyContracts`, `getMyDeliveries`, `getMyMarketState`, `getMyAcquisitionOffers`, `getMyWorldEvents`, `getMyBankState`.

Request allowlist: `{protocolVersion:1}` plus optional `limit`/`after` where the contract says so. Envelope: `{protocolVersion:1,serverTime,result}`. Unknown response fields are tolerated. Unknown request fields are not sent.

## Command callables (documented, not invoked in CLIENT-API-01)

`ensurePlayerIdentity`, `createCompany`, `acceptContract`, `startDelivery`, `claimDeliveryReward`, `buyVehicle`, `hireDriver`, `acquireTrailer`, `openWarehouse`, `upgradeWarehouse`, bank loan commands.

Financial commands are never auto-retried.

## Holds

- CONNECTION HOLD: no emulator or production call without a new user approval.
- AUTH HOLD: no sign-in / UID bootstrap in this milestone.
- ROAD FILTER CONTRACT HOLD: `getMyDeliveries` has no `transportMode`.
- Region-chip overlap UX debt on the physical iPhone.
- Footer Jobs CTA TOUCH HOLD.
- PlayerSettings orientation HOLD.
- General backend security audit HOLD; worker reliability HOLD; production/deploy/migration BLOCK.

Backend contract sources live under `docs/api-contracts/`. Do not copy TypeScript business logic into C#.

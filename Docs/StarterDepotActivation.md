# START-DEPOT-02 — Server-validated first storage depot and starter operations state

## What was implemented
- **Versioned, synthetic-only warehouse fixture:** backend/functions/src/devStarterDepotCatalog.ts introduces immutable catalog ID lc_dev_starter_depot_v1. It extends the existing vetted eight-city v2 starter fleet fixture with one level-1 starter_depot warehouse template per supported city. The trial storage capacity is 24 pallet slots, capacity model pallet_slots_v1. It does not modify any previously issued catalog version or wallet state.
- **Explicit status read:** getMyStarterDepotProgress reports the authenticated company's actual ordered prerequisites: no company → legacy HQ missing → site missing → economy pending → unsupported starter region → starter fleet pending → warehouse catalog pending → ready to activate → operational warehouse with dispatch pending. Inactive state is never presented as operational.
- **Atomic, idempotent command:** activateHeadquartersStarterDepot takes exactly an empty payload (the server chooses the real company, HQ city, warehouse type and capacity). It only creates a warehouse under the existing warehouses collection and an immutable activation marker when:
  - user owns the authenticated company and HQ state is valid;
  - the original depot site is reserved for exactly the same location;
  - economy is initialized with the existing authoritative finance/progression documents;
  - the existing starter truck and driver package is truly provisioned and validated;
  - the city is one of the existing eight Turkish starter-catalog cities;
  - a strict, matching server-seeded lc_dev_starter_depot_v1 catalog exists.
- **No duplicate depot:** old/different-key repeat attempts fail; an identical idempotency key returns the original receipt. A mismatched warehouse or activation marker is rejected without auto-repair. The warehouse and activation marker commit together. No grant of starting truck/driver is made in this command.
- **Capacity is real game state:** created warehouse uses the existing validateWarehouse schema, usedCapacity=0 and an initial 24 pallet slots. Do not display occupied slots or forklifts until future cargo and stock states exist.
- **No route or monetary fabrication:** warehouse activation is a one-time starting storage entitlement without a balance change, receipt ledger cash mutation, route creation or contract issuance. dispatchReady remains false. Verified KGM city-center distances and the existing synthetic eight-city route fixture are not drivable road geometry.
- **Client/UI:** CompanyStarterSiteStatus now reads getMyStarterDepotProgress and presents authentic stage-specific TR/EN/DE status text in a restrained two-level hierarchy; it offers an explicit depot activation action only after the verified ready_to_activate stage and only in an approved local Firebase emulator company test window. It makes a fresh trusted read after successful activation instead of immediately claiming success.
- **Security:** ClientCommandAuthorization.AllowsLiveFunctions remains false. The new gateway only recognizes the approved loopback demo-logisticore-local emulator + signed-in UID + explicit action and company test opt-in. The backend is still emulator-only and old companies/players are untouched.

## What is NOT active yet
- No automatic economy initialization, starter-fleet provisioning, and no production backend rollout. Those are separate, existing commands with separate validation requirements.
- The new warehouse catalog is **not automatically seeded** into Firestore, by design. It must be deliberately provisioned **only in the isolated emulator** after approving its exact versioned contents and before any activation. Missing catalog returns awaiting_warehouse_catalog, never an invented successful activation.
- No operational warehouse can currently be activated in the other 73 Turkish provincial cities or 167 initial European cities, because their depot/fleet coverage and route permissions are not yet supported. Their starting depot **site remains reserved**. This is a real limitation to resolve before a balanced international launch.
- No physically rendered/interactable 2.5D depot yard has been shipped. This PR upgrades authoritative backend states and the company status card, **not** final building art. No fake busy yard graphics.
- No live freight contracts, road polylines, border-crossing rules, dispatch, payment, premium currency or warehouse upgrades.
- Tests are written but **not executed** yet at the agreed final verification milestone.

## Source contracts
- POST callable activateHeadquartersStarterDepot: protocolVersion 1, idempotencyKey, payload {} only, signed-in emulator account. Same ownership and receipt model as createCompanyWithHeadquarters. Activation result includes warehouseId, city identity, capacityModelId, capacityUnits, stage warehouse_operational_dispatch_pending_routes and dispatchReady false.
- GET callable getMyStarterDepotProgress: read request {protocolVersion:1}; account status + location identity and optional warehouse record, no writes.
- v2Companies/{companyId}/warehouses/{warehouseId}: existing schemaVersion 1 warehouse record, catalog pinned to lc_dev_starter_depot_v1.
- v2Companies/{companyId}/state/starterDepotActivation: separate immutable evidence marker (owner, company, HQ city, warehouse ID, catalog version/hash, status, activatedAt). The original state/starterDepotSite reservation is NOT mutated.
- v2Players/{uid}/commands/{idempotencyKey}: existing atomic command receipt.

## City expansion for European audience
Before offering a real HQ game loop for Berlin, Paris, Rotterdam or other European starting cities, provide:
1. A vetted city-specific depot/equipment catalog and consistent starting economy; country is not a difficulty penalty.
2. A legally usable road-route service with verified geometry, times and border/vehicle restrictions, or show delivery unavailable for disconnected locations.
3. Server-validated starter fleet and first contract offer in that city. No hidden pay-to-unlock a region where a new player starts.
4. Correct TR/EN/DE label localization and country-appropriate signage/architecture without fake landmarks.
5. Source and licensing records for roads and authored visual assets.

## Design quality gate
See Docs/VisualDirection/LogistiCoreVisualQualityBar.md. The only new visible UI here is a restrained real-data operations panel. Future depot art must be modular, authored, coherent in 3/4 perspective, and driven by verified operations. Previously introduced procedural skyline placeholders do NOT count as launch-quality unique city visuals.

## Final verification (not yet performed)
- TypeScript build/typecheck, unit tests, Firebase emulator integration and strict Firestore rules tests.
- Unity compile, EditMode, PlayMode, localization widths, Android/iOS layout and performance.
- Legacy saves, idempotent replay, account switch, corrupted catalog/warehouse, incomplete economy, missing starter fleet, unsupported Europe city, and route-provider-outage regression.
- No merges to main, no production Firebase deploy and no gameplay permission changes until all checks pass.

This is a stacked Draft PR on START-DEPOT-01 (#33).

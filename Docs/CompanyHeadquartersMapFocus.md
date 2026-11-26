# CITY-START-04 — Trusted company headquarters → opening map

## Implemented in this draft
- A lightweight `MapCompanyHeadquartersFocus` controller is installed on the existing map camera during the `MapTradeRegionMarkers` runtime overlay setup. Existing scenes, map projection, original eight map markers and coordinate IDs remain unchanged.
- After the map scene finishes `Start` and **only** if an authenticated user and the exact approved local Firebase emulator connection are ready, the controller makes **one read** to `getMyCompanyHeadquarters`. It uses the existing read-only client invoker, client envelope parser and pinned HQ catalog version; it never sends a command or writes to Firestore.
- A successful, trusted record must identify an existing company, contain a country-scoped city key and a supported catalog version, resolve in `MapGlobalCityCatalog`, pass the `MapTradeRegionCatalog` Türkiye+Europe launch filter and project through `MapProjection`. The camera then switches to Turkey or Europe scope and focuses that catalog coordinate, opening the existing city inspector.
- For one of the original eight Turkish cities, the inspector uses the original `ShowCity` path; all other valid cities use `ShowGlobalCity`. Old city labels, markers and navigation remain intact.
- No company, missing headquarters record on a pre-existing company, a malformed/unknown/off-region city, backend refusal, missing authentication or unavailable Firebase connection **never fabricates a home city** and **never moves the existing map**.
- `MapHeadquartersFocusState.LegacyUnassigned` distinguishes a historical company without a server-side HQ sidecar from a brand-new account and from a failed network read; this is exposed to UI but does not display a new message yet.

## Priority and race handling
1. The existing **manual map preview selection** from the company scene is consumed in `MapTradeRegionMarkers.Start` and always takes precedence over automatic home focus.
2. Camera gestures (tap, pan, touch pinch, mouse wheel) cancel delayed HQ focus. Region switches fire `MapCameraRig.ScopeChanged` and also cancel it. The existing camera `UserAdjusted` flag catches toolbar zoom controls that bypass raw gesture handling.
3. Scene-initialization scope changes are ignored before the controller's `Start`. A late Firebase response is ignored after user navigation, scene destruction, authentication change or a higher-priority manual preview.
4. Only the first successful read can auto-focus per map scene entry. The controller does not continually snap back to HQ; users can explore freely after focus.

## Strict environment limitations
- Backend `getMyCompanyHeadquarters` is the local emulator-only implementation from CITY-START-02; current `backend/functions/src/localEnvironment.ts` refuses non-demo environments. Production players do **not** receive an authenticated HQ read from this draft. Default unconfigured app builds keep the previous Turkey map opening behavior.
- No modifications to `v2Companies`, existing account documents, company creation command, economy, warehouse ownership, player wallet, active contracts or shipment routes.
- For legacy companies with `hasHeadquarters:false`, the internal `LegacyUnassigned` state is available for a later explicit UI notice/migration decision. No city is chosen or written based on the device country/GPS or the last viewed location.

## Code touched
- `MapCompanyHeadquartersFocusPolicy.cs` — pure safe resolution of trusted HQ data to map coordinates and status.
- `MapCompanyHeadquartersFocus.cs` — scene-attached, one-shot authenticated read, camera positioning and cancellation.
- `MapTradeRegionMarkers.cs` — attach focus controller and track explicit preview precedence.
- `MapGestures.cs` — signal user map navigation so late server responses cannot hijack the camera.
- `MapCompanyHeadquartersFocusTests.cs` — deferred EditMode coverage for new, legacy, valid Turkey/Europe and invalid/off-region homes, navigation cancellation and read-only authorization boundaries.

## Next development
1. Define and authorize actual company-owned **initial depot** for a selected HQ city; do not create depots just because a point is visible in the map catalog.
2. Connect a provider-backed **road-network route quote and cached geometry** to verified delivery contracts (ROUTE-01), with distinct access permissions for country delivery and branch purchase.
3. Add visible/fully localized HQ status where appropriate, using `LegacyUnassigned` rather than guessing for existing accounts.
4. Final validation: compile the stacked Unity project, run EditMode/PlayMode tests, local Firebase emulator tests, screen layout and save-migration regression on Android/iOS. **Not executed in this draft by prior agreement.**

This is a **stacked Draft PR** based on CITY-START-03, not independently mergeable into `main`.

# START-DEPOT-01 — Initial HQ depot site, not an operational warehouse

## Delivered in this draft
- A new company created through the versioned, emulator-only \`createCompanyWithHeadquarters\` command now receives exactly **one reserved initial depot site** in **the same Firestore transaction** that creates the company and stores its verified HQ city.
- Record: \`v2Companies/{companyId}/state/starterDepotSite\`. Its city ID is copied from the server-validated \`headquarters\` payload/allowlist and is immutable, matching the company owner's UID and company ID. It has a pinned launch catalog version, an immutable \`site_reserved\` status, and an explicit \`unavailable_pending_verified_routes\` dispatch status.
- Replaying creation with the same key reproduces the recorded receipt. New creation with a different key fails because the company already exists. No user-supplied balance, warehouse catalog, route, vehicle, location coordinates or operational status is accepted.
- Existing accounts/legacy \`createCompany\` remain unchanged. Old companies without an HQ record receive **no** fabricated site. Earlier emulator-created HQ companies without a site also remain valid and show \`hasStarterSite:false\`; they are not silently mutated.
- A separate authenticated, read-only \`getMyStarterDepotSite\` callable checks the server's immutable HQ record and site record together. A mismatched owner, company, city, catalog version or forged operational status fails closed with \`COMPANY_STATE_INVALID\`.
- The Unity client adds a strict \`ClientReadParser.TryStarterDepotSite\`, a protected read-only callable name and a localized EN/TR/DE company-panel status. After creating a new company the panel refreshes the status from the server.
- Backend unit, emulator and Unity EditMode **deferred** test contracts cover atomic site creation, replay, legacy preservation, malformed/tampered site records, direct-client Firestore default deny and offline presentation.

## What this is NOT
- **Not an operational depot/warehouse**. It does not add a document to the \`warehouses\` collection, and is not reported as owned warehouse capacity.
- **Not a fleet**. No truck, driver, trailer or starter package is created by the HQ site reservation. The existing \`provisionCompanyStarterFleet\` command and economy initialization continue to be separate, server-controlled operations.
- **Not a route or contract**. There is no guessed road geometry, travel time, toll or cargo job; the site explicitly states that dispatch awaits verified road and fleet eligibility.
- **Not a price/payment**. The initial site is a non-tradable, no-fee reservation, not an investment transaction or money-credit. Existing company balances and warehouse purchase offers remain untouched.
- **Not a production rollout**. Everything continues to be limited by \`assertLocalEnvironment\` to \`demo-logisticore-local\` loopback Firebase emulators; Unity command dispatch is still owner-gated and default-deny. This is not connected to live player accounts.

## Why reserve before activating?
The Türkiye + Europe opening city catalog has 248 locations, but the currently validated operational road/contract fixture and logistics catalog still cover only eight Turkish cities and have no starter warehouse definitions. Creating 248 "operational" warehouses or giving foreign HQ cities first contracts would be incorrect. The site record gives the game a **safe, stable facility anchor** for map-to-yard navigation while operational entitlements wait for the actual supported road network and catalog.

## Next — START-DEPOT-02, ROUTE-01
1. Define the versioned operational starter-warehouse catalog (city-scoped capacity/model, no fake region-specific warehouse) and explicit activation eligibility tied to validated road routes. Keep distinct states for *reserved site*, *active warehouse*, *starter fleet provisioned*, *dispatch available*.
2. Adapt the already-existing, idempotent \`provisionCompanyStarterFleet\` to work with launch country-specific eligibility without granting assets before the economy is initialized or the catalog pin is validated. Never duplicate a starter package.
3. Expose a verified onboarding progress panel and map-to-depot entry that distinguishes a reserved location from an operating logistics yard.
4. Add licensed provider-backed Türkiye+Europe road route quotes and enforce country/vehicle/contract permissions server-side. Regions with no valid route must remain pending rather than showing imaginary deliveries.
5. Perform final CI/Unity TypeScript compilation, Firebase emulator unit/integration tests, EditMode/PlayMode, Android/iOS layout, and account/save migration checks before merging. **Not run as part of this agreed code-first draft workflow**.

## Compatibility
- The schemaVersion 1 company document, economy/asset/warehouse documents, default map, and all existing company data are unchanged.
- Only new HQ companies created via the separate new command receive the extra reserved site record.
- Legacy no-HQ and HQ-without-site are both valid, explicit, non-destructive read states.

All changes in stacked Draft PR; not independently mergeable to \`main\`.

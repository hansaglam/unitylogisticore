# CITY-START-01 — Country / city headquarters map preview

## Delivered
- Existing `Company` portrait screen gains a country selector and a city selector fed by `MapGlobalCityCatalog` and restricted to `MapTradeRegionCatalog` (Türkiye + Europe, currently 248 cities in the stacked MAP-06 data).
- Country labels use the existing `MapCountryCatalog` English/Turkish/German names. Control copy is translated to EN/TR/DE; imported geographic city names still need full localized endonyms.
- Clicking `Preview on map / Haritada incele / Auf Karte ansehen` stores **one short-lived, one-shot navigation intent** in memory and opens the existing landscape map scene.
- Runtime regional marker overlay consumes the intent, focuses the map on Türkiye or Europe and positions the camera on the exact catalog city coordinates. The existing city information panel opens at that city.
- The existing world map scene, eight original Turkish map markers, their IDs and coordinates, all company account data and the Firebase backend are unchanged.

## Critical boundaries: this does not yet create or move a company's HQ
- The deployed backend `createCompany` command in `backend/functions/src/company.ts` validates **exactly one payload field: company name**. Existing `v2Companies` documents are schemaVersion 1 and `readFoundation` rejects unrecognized company fields. This is **not** compatible with a client-only HQ field.
- Therefore this feature **does not add a headquarters field**, modify the company-creation payload or write to Firebase. Calling the selection a confirmed HQ would mislead existing players.
- The visible screen explicitly labels the feature a map preview and tells users it does not create a company or change their headquarters.
- Preview state is transient, cannot be restored after app restart, and is removed after the next map entry. It has no gameplay authority, route entitlement or money logic.

## Next requirements before "Create company here" can be activated
1. Introduce a **versioned, server-validated company creation contract** accepting a country-scoped city ID that exists in the authorized launch catalog, along with existing company name; preserve idempotency semantics.
2. Design migration and read compatibility for all existing company documents and legacy saves, with a safe no-change path for existing accounts. Avoid adding new fields to schemaVersion 1 documents without migration (current validator will reject them).
3. Provide authenticated `getMyCompanyState` / HQ state to Unity. Do not auto-select a new home city for existing accounts.
4. Add localized HQ confirmation/review UI that only enables server writes when the validated backend contract is available.
5. Determine access and initial depot rules for valid but non-road-connected locations and international starting countries, with balanced economics and real provider-based route availability.
6. Run final Unity compile, EditMode/PlayMode tests, Android/iOS layout and backend migration tests before merging stacked Draft PRs.

## Technical notes
- A normal `CompanyShell` scene remains portrait; map scene continues to use `AppNavigation.ShowMap` landscape.
- Use standard uGUI dropdown templates for long country/city menus; scrollable company column remains bounded by the safe area.
- New code lives in `CompanyHeadquartersPreview`, `MapHeadquartersPreviewSelection` and a small runtime marker focus handler.
- Existing shell test expectation now includes the new preview button. Saved NUnit regression checks are intentionally **not run yet** per requested test schedule.

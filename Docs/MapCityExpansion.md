# MAP-03 — World city network and company expansion (design contract)

## Product rules
- The world map is always explorable. Cities are not locked behind real-money payments.
- Headquarters is selected from supported cities when a company is created. Existing players retain their current headquarters and company state; never reset saves.
- **Delivery access** is independent of **branch ownership**. A player may deliver to a city without opening a depot there. An owned depot does not automatically create a valid contract.
- A **branch opportunity** can be shown when the server reports eligibility. Branch construction requires a verified in-game business investment and/or a career milestone. The business economy team may offer alternative unlock paths; their costs and conditions are server-defined, not hard-coded in Unity.
- **International expansion** may depend on verified country permissions, contracts, fleet capabilities, milestones and business resources. Presentation-only permission indicators cannot authorize shipments or purchases.
- New activities should be earned by company progression, not artificial real-time waiting gates or required premium purchases. City/region variation can provide new cargo categories and environmental identity without forcing a new warehouse for every delivery.

## Read model introduced in this PR
`MapBusinessLocation` = (ISO alpha-3 country code, stable *unique* city key). Do not rely on display names or country+city-name alone as a universal identifier; duplicate city names exist within countries.

`MapCityNetworkSnapshot` contains **explicit trusted** lists of delivery destinations, eligible branch locations, owned facilities and approved countries. `MapCityNetworkAccess.Evaluate` returns independent read-only flags and a summary stage for future world map city panels.

These lists must be populated from server-validated data. Until backend endpoints support them, do **not** populate them with guessed routes, fake business ownership or hard-coded unlocks; show an unverified state instead. This feature does not change any Firebase data and does not implement spending, company creation, branch purchases, route planning or shipment authorization.

## Backend integration needed next
1. Adopt a vetted global populated-places catalog with stable city keys, coordinates, country codes and localization fallback; preserve legacy Turkish city IDs via explicit mapping.
2. Define account migration for the existing headquarters, owned depots and contracts before changing DTOs.
3. Expose verified country access, available delivery destinations and branch eligibility/ownership in authenticated reads.
4. Implement idempotent server commands for unlocking permitted expansion and purchasing branches, validating company balance and milestone completion; handle optimistic UI as a pending state, not a granted unlock.
5. Feed map panels with read-only statuses; let contract availability and equipment compatibility determine whether a delivery can be started.
6. Add regional/world marker clustering, viewport filtering and a map-to-facility hub transition, reusing the existing projection and camera rig.

## Validation milestone
Unity compilation and EditMode/PlayMode tests, localization (TR/EN/DE), account-save migration, Android/iOS performance and no-unauthorized-economy-change regressions are deferred to the agreed final review.

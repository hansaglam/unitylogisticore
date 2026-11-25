# MAP-05 — Turkey + Europe launch geography

## Implemented in this draft
- **Türkiye**: 81 provincial center points. Eight previously shipped marker coordinates/IDs are unchanged; 73 further province centers were imported from MIT-licensed TurkeyProvinces (see `Docs/ThirdParty/TurkeyProvincesLicense.md`).
- **Europe**: current Natural Earth 110m seed provides **47 additional city points in 45 countries/territories**, including western Russia where geographically appropriate. This is a **starter coverage**, not yet the planned 60–100+ important logistics centers; smaller cities remain to be imported from a licensed higher-detail source.
- **Total geo catalog**: 322 worldwide cities/points. **Opening operating region**: 128 cities (81 Turkish + 47 other); rest kept for future expansion and are excluded from the initial map overlay.
- Existing scene, Mercator projection, camera and original eight marker components stay intact.
- During play, `MapTradeRegionMarkers` is automatically attached to the existing marker layer. It creates lightweight pooled sprite markers from existing map art, only when camera scope is Turkey or Europe. Turkey scope limits to Turkish points. Europe scope allows Turkish + initial European countries. World/other continent scopes hide the overlay.
- Markers are culled outside the camera and clustered in 40px screen-space cells. Tap a cluster to zoom toward it; tap an individual point to see the existing city inspector with display name, geographic coordinates and appropriate stylized regional banner.
- The original eight markers retain their precise tap targets and localization. Additional city names currently display the geographic source name; the complete TR/EN/DE name pass is **not done**.

## Product presentation
- Startup currently keeps the **Türkiye** view for compatibility with earlier scene camera tests. The existing **Europe** region control reveals the wider initial operating region.
- City dots are **neutral** until authenticated backend access states are available. Do not show them as purchased, unlocked, owned or associated with available contracts.
- All map cities are browseable in the opening region, but seeing a city is not authorization to ship there or build a depot. Contract destinations and branch purchase states must come from the trusted backend.
- Existing 8-city `MapSchematicLinks` are legacy artistic lines, **not roads**. Their presentation is retained on the original Turkey overlay until ROUTE-01 replaces it with verified route polylines. Do not use these links for economy or dispatch.

## Not yet included
- Full Europe city details (target 60–100+ hubs), full provincial districts, localization of city names, headquarters-selection migration or a world-country-selector refresh.
- Route provider, road-network geometries, truck tracking from authoritative routes, contract-generation changes, country unlock logic and depot scene navigation.
- Unity compilation, EditMode/PlayMode and device tests are **deferred** to the agreed final validation milestone; the draft is not yet production-verified.

## Remaining map/route integration
1. Source a **licensed 50m/10m populated places or other verified Europe city list**, map entries to stable IDs and enrich city labels.
2. Load trusted headquarters, contracts, accessible destinations and owned facilities to apply server-derived marker states (distinct symbols for exploration/delivery/branch/owned).
3. Replace purely artistic network lines with actual provider-derived road polyline renderer and country-aware road availability.
4. Begin hub scene: map → owned facility → logistics yard → map, keeping selected city identity.
5. Run final regression and migration checks prior to any merge/release.

## Geography & licensing
- Natural Earth geographic source: https://www.naturalearthdata.com/downloads/110m-cultural-vectors/110m-populated-places/ (public domain).
- Turkish province-center data: https://github.com/halilozel1903/prayer-times-kotlin (MIT license included).
- Transport routes will require a separate licensed/hosted road-routing backend (OSM-based OSRM/Valhalla or suitable contracted provider); city point data is **not** routable geometry.

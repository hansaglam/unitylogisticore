# MAP-05 — Turkey + Europe launch geography

## Implemented in this draft
- **Türkiye**: 81 provincial center points. Eight previously shipped marker coordinates/IDs are unchanged; 73 further province centers were imported from MIT-licensed TurkeyProvinces (see `Docs/ThirdParty/TurkeyProvincesLicense.md`).
- **Europe**: Natural Earth 110m baseline plus curated 50m populated places now provides **167 European city points across 45 country/territory codes**, including western Russia where geographically appropriate. City density varies markedly by country (Germany currently five; France and Italy more than twenty each). Future logistics hub enrichment should be based on demand/road-connectivity, not merely overall point count.
- **Total geo catalog**: 442 worldwide cities/points. **Opening operating region**: 248 places (81 Turkish + 167 other); the rest remain preserved in the library but are excluded from the initial Turkey-Europe game overlay.
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
- Balanced hub density for key European freight markets (notably Germany, Benelux and Poland), district-level Turkey data, localization of city names, headquarters-selection migration or a world-country-selector refresh.
- Route provider, road-network geometries, truck tracking from authoritative routes, contract-generation changes, country unlock logic and depot scene navigation.
- Unity compilation, EditMode/PlayMode and device tests are **deferred** to the agreed final validation milestone; the draft is not yet production-verified.

## Remaining map/route integration
1. Curate remaining high-value road freight hubs in Germany, Benelux, Poland and the Balkans from a vetted/licensed more detailed catalog, map entries to stable IDs and enrich city labels.
2. Load trusted headquarters, contracts, accessible destinations and owned facilities to apply server-derived marker states (distinct symbols for exploration/delivery/branch/owned).
3. Replace purely artistic network lines with actual provider-derived road polyline renderer and country-aware road availability.
4. Begin hub scene: map → owned facility → logistics yard → map, keeping selected city identity.
5. Run final regression and migration checks prior to any merge/release.

## Geography & licensing
- Natural Earth geographic source: https://www.naturalearthdata.com/downloads/110m-cultural-vectors/110m-populated-places/ (public domain).
- Turkish province-center data: https://github.com/halilozel1903/prayer-times-kotlin (MIT license included).
- Transport routes will require a separate licensed/hosted road-routing backend (OSM-based OSRM/Valhalla or suitable contracted provider); city point data is **not** routable geometry.

## MAP-06 Europe city data provenance
- Natural Earth 50m `ne_50m_populated_places_simple.geojson`, fetched 2026-11-26: https://github.com/nvkelso/natural-earth-vector/blob/master/geojson/ne_50m_populated_places_simple.geojson (public domain).
- 167 European source points considered; 47 duplicated previously catalogued cities and retained their stable legacy identities, while 120 additional points were added with `ne50-<Natural Earth ne_id>` IDs.
- New European places use the same runtime marker/cluster and city inspector from MAP-05, but **are not automatically eligible for freight, branch expansion or a new headquarters**. These need trusted server permissions and onboarding UI.
- No city names have yet been translated or validated against regional endonyms. No Unity run, map screenshot, or Android/iOS device test was done in this draft.

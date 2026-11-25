# MAP-04 — Worldwide cities and verified routes (first seed)

## What is present
- A **global initial city catalog** generated from the public-domain Natural Earth *110m populated places* dataset (Natural Earth v5.x, fetched 2026-11-26): 243 source places plus six additional legacy Turkey markers, **249 total** spanning 198 country/territory codes.
- Every catalog entry has a country code, unique/stable city key (based on Natural Earth's `WIKIDATAID`), name, geographic coordinates and cartographic scale rank. The eight existing Turkey marker IDs and coordinates are preserved exactly.
- `MapGlobalCityCatalog.TryGet(MapBusinessLocation, out MapGlobalCity)` resolves city metadata without claiming the city is selectable, owned, licensed, contracted or road-connected.
- `MapVerifiedRoute` is a *presentation-level* structure that accepts backend-validated route id, provider name, mode (road/rail/sea/air), origin/destination, distance, duration and geographic route polyline. It projects this polyline through existing `MapProjection` without changing the world map.
- Route geometries crossing the antimeridian require segmented rendering and currently fail closed instead of drawing a false straight line across the world.
- New test contracts cover geographical identity, legacy migration preservation, unverified route rejection, malformed geometry and Mercator display projection; intentionally not executed yet.

## What is NOT present
- **NOT the complete worldwide cities database**. This first cartographic tier has 249 points. Next source tier should use vetted Natural Earth 10m (7,000+ points) or a GeoNames populated places export for broader coverage. GeoNames data is CC BY 4.0 and requires attribution.
- **NOT yet displayed as 249 clickable map markers.** The serialized existing map marker layer remains eight Turkey markers. Need a data-driven overlay with zoom-aware LOD, viewport culling, clustering, interactive hit-testing and localized names.
- **NOT any live road route calculations.** Existing `MapSchematicLinks` are *illustrative only* and must not be used for contract distances or earnings.
- **NOT backend route validation** and not a route provider integration. `serverValidated` is an API/model precondition, not proof a client can create a valid payable shipment. On-device route previews never authorize money, contracts or permissions.
- NOT client-generated roads, self-generated cargo jobs, prices, route times or bypasses around border/transport restrictions.

## Global route architecture
1. Choose a headquarters from permitted cities, preserving all existing player progress.
2. Map catalog resolves country/unique city ID and WGS84 lat/lon. City marker rendering is zoom-aware and only displays relevant points.
3. Available destinations and contract offerings come from authenticated backend data. A visible city does not automatically become an unlocked destination.
4. For a proposed **road** shipment, the backend (or authorized routing service behind the backend) asks an OSM-based route engine such as a self-hosted OSRM/Valhalla instance or a contracted routing API for **road-network geometry, actual routable distance and travel-time estimate**. If no connected drivable route exists, road mode is unavailable; use rail/sea/air only when their dedicated systems exist.
5. Cache quotes server-side by source/destination, vehicle profile, route mode, engine version and expiry. Do not calculate every N×N pair or call a paid route API on every frame.
6. Client uses the returned route polyline for animation and visual tracking, but the server authorizes delivery, border access, equipment, idempotent costs, milestones and completion rewards.
7. Visual progress along the route derives from an authoritative start/end state, interpolated locally. Do not move a truck by assuming a direct great-circle line, or bill based on on-device distance.
8. Test antimeridian route segmentation, country borders, ferries, islands, disconnected networks, multimodal legs and route provider outages before production.

## Provenance and licensing
- Natural Earth, *ne_110m_populated_places*, https://www.naturalearthdata.com/downloads/110m-cultural-vectors/110m-populated-places/ ; public domain. Coordinate and city name source: https://github.com/nvkelso/natural-earth-vector/blob/master/geojson/ne_110m_populated_places.geojson.
- Existing eight Turkish city markers remain Natural Earth 10m (see `MapCities`) for continuity.
- The OpenStreetMap map database is ODbL licensed, with attribution and relevant use obligations: https://www.openstreetmap.org/copyright .
- OSRM project: https://project-osrm.org/docs/ . **Public OSRM demo servers are not production infrastructure for a commercial mobile game**; choose self-hosted or licensed provider.
- GeoNames for later expanded cities: https://www.geonames.org/export/ (CC BY attribution).

## Next integration milestone
**MAP-05:** Import deeper city data; runtime marker layer with bounded/clustered viewport markers; connect selected city to city panel without mislabeling routes. **ROUTE-01:** Add backend-controlled OSM road routing quote resolver/cache, trusted DTO parser and distinct route renderer. **HUB-01:** link company-owned facility selection to depot scene and persist return to map.

Keep all changes in stacked draft PRs until the agreed Unity regression + Android/iOS and existing-player migration validation milestone.

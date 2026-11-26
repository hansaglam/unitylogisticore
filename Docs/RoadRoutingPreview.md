# ROUTE-01 — Read-only road preview (Turkey + Europe)

## Implemented in Draft
- A server-owned WGS84 coordinate snapshot for exactly 248 permitted Turkey–Europe launch city keys (81 Turkey, 167 European cities), derived from the existing Unity map and headquarters catalog.
- A read-only Firebase emulator callable called getMyRoadRoutePreview. It verifies the authenticated user and company HQ and allows preview only FROM that verified HQ TO a recognized launch city. Missing HQ, unknown city and cross-account origin fail closed.
- An OSRM adapter restricted to a FIXED local-only HTTP origin at http://127.0.0.1:5055 . It sends [longitude,latitude] pairs to the documented route/v1/driving endpoint with GeoJSON geometry, simplified overview, no steps and no alternatives.
- Validates OSRM's single returned GeoJSON route: positive bounded distances (meters) and durations (seconds), 2–4096 finite geographic points, endpoints near authorized city centers, and no antimeridian crossing. A disconnected road returns no_road_route, while missing/unhealthy service returns RETRY_LATER. Never creates a fictitious straight-line route.
- Small 64-entry / 2-minute local in-memory geometry cache. No route requests from map camera changes or while idle.
- Every successful map read is explicit about purpose=map_preview_only, verification=geometry_only and dispatchReady=false. The route identifier is SHA-256 derived from response geometry and endpoints. Distance/time are NOT used for fuel, contracts, reward or pricing.
- Unity: MapRoadRoutePreviewParser validates the exact read DTO, MapRoadRoutePreviewLine draws the provider line at restrained ~3 px on the existing Web Mercator map, MapRoadRoutePreviewController reads company HQ before requesting a route. The city inspector shows an explicit, translated (TR/EN/DE) preview action only with approved local Firebase emulator reads. The visible route includes OpenStreetMap attribution and the no-dispatch warning.
- Local-only origin preview is not a heavy-goods or border permit, and does not activate any depot. Old eight-city contracts, KGM distances, company data, paid commands and the main branch are untouched.

## Not yet operating
1. **OSRM is not installed or running as part of this PR.** The developer must configure a self-hosted OSRM driving server on 127.0.0.1 port 5055 with appropriately licensed OSM road data. If no service or no connected road exists, no route appears.
2. The public OSRM demo server and public OpenStreetMap tile infrastructure are not used as a commercial backend. A production routing provider needs separate approval, traffic budgets, monitoring, live caching and legal compliance.
3. The standard driving profile is NOT HGV/truck-certified routing. Truck height, weight, ferry constraints, road access, toll, national border and legal driving/rest limits are not modeled here.
4. These previews are never dispatch authorizations, fuel estimates, contracts or cash events. Existing signed contract and delivery systems continue to use their prior data models.
5. No decorative/fake moving trucks or unrelated landmarks have been added. The route line is visually distinct from MapSchematicLinks' artistic connections.
6. Tests are WRITTEN BUT NOT EXECUTED. Unity compile, device layouts, TypeScript build, Firebase emulator tests and real provider integration remain scheduled for the final verification milestone.

## Integration and attribution
- Official OSRM HTTP API: https://project-osrm.org/docs/v26.6.1/http
- OpenStreetMap attribution and license: https://www.openstreetmap.org/copyright
- OSM data may be used under ODbL terms; credit © OpenStreetMap contributors when rendering derived route geometry.
- ROAD-02 will require company/fleet/warehouse eligibility, provider-backed truck-specific route profiles, toll and border authorization, server-priced quotes and trusted active-delivery motion before shipping is playable.
- DEPOT-VISUAL-01 will build one authored mobile 2.5D yard/depot scene reflecting real reserved/storage/dispatch states, not generated idle traffic.

This is a stacked Draft PR based on START-DEPOT-02; never merge it to main independently.

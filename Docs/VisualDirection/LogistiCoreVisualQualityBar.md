# LogistiCore — Premium 2D/2.5D Visual Quality Bar (locked for the Turkey + Europe launch)

## Creative rule
LogistiCore must look and behave like a **designed logistics management game**, not an AI-image collage or a generated administrative dashboard. Every visual element must either communicate the business state, clarify a decision, distinguish a place for a grounded reason or reward a completed player action.

**Visual identity:** premium, grounded, restrained mobile logistics-tycoon. Reference the existing midnight-navy, muted blue-grey and measured cyan map/HUD palette; maintain one authored family of building shapes, truck silhouettes, shadows, road markings, UI typography and icon line weights. Realism of operation matters more than photographic textures.

## Site progression must be visible and truthful
| Authoritative state | Grounded site appearance | Player affordance |
|---|---|---|
| No company / no headquarters | Existing map view; no invented building or owned-warehouse icon | Company registration and map exploration |
| Site reserved | Surveyed logistics parcel and surveyed access/lot markings ONLY when the site scene exists | View location; review missing prerequisites |
| Economy + starter truck/driver ready, warehouse not activated | Warehouse proposal / construction-ready outline, NOT a functioning depot | Explicit, server-approved activation action |
| Warehouse operational | Small authored warehouse footprint; storage slots/capacity shown as a concrete numeric resource | Manage site and view real capacity; route dispatch still disabled |
| Route + shipment authorization eventually verified | Roads and truck motion follow authoritative actual shipping leg state | Accept/dispatch/track genuine deliveries |

**Non-negotiable:** 24 pallet slots are a *test-gameplay unit*, not a real-world architectural size. Do not render 24 loaded pallets or busy forklifts without cargo state. A route cannot be animated with a fake straight line across the map. A country/route can be geographically visible and still not open for business.

## Authored facility kit, not generated variety
Initial hand-designed component kit (future asset sprint; not delivered in START-DEPOT-02):
- Warehouse mass with one coherent 3/4 projection (roof, walls, shadows, dock bay).
- Paved yard, gate, loading apron, road approach and parking bays on an aligned fixed perspective.
- Actual truck and trailer variants within the same view angle, wheelbase and scale; no rotating pseudo-3D models that change visual language.
- Loading dock occupancy, traffic lane markings, pallet area and a restrained selection outline.
- Business state markers (reserved, working, blocked) as a consistent icon family; no fantasy reward particles or glowing warehouse trophies.
- Context dressing by region (road sign conventions, landscape climate and infrastructure), with location-specific references and license/provenance noted. Never drop arbitrary landmark collages behind every city.

For a future larger city set, build **limited curated archetypes** and meaningful variations in building, yard configuration and locale; procedural skyline silhouettes already in the map code are placeholders, not accepted final city identity art. Do not treat seeded procedural shapes as proof the world has complete city-specific visuals.

## Motion and gameplay synchronization
- Truck moving = an actual assigned route/delivery in trusted game state. No endless looping traffic made to look active.
- Forklift loading = an actual loading operation with real quantity and corresponding occupancy. Idle warehouses should visibly rest.
- Construction/activation = authoritative state transition, ideally low-key 0.2–0.35 s shape/position animation rather than a fake cinematic reveal.
- UI progress bars = server state or deterministic calculation, never fabricated "production" counters.
- No visually plausible but non-interactive fake buildings, buttons, roads or charts.

## UI quality bar
- Reuse ShellPalette and existing app typography. **One dominant action per screen**, meaningful hierarchy and sufficient touch targets.
- For the starter depot, show only two levels: **DEPOT** heading and actual operational prerequisite/status; activation button is shown only when a trusted server state says it can be requested.
- No busy gradient cards, icon confetti, stock imagery, incoherent corner radii or numerous colored badges. Avoid overdesigned fake dashboard statistics.
- TR, EN and DE labels must be checked against real device widths. If labels wrap, adapt layout instead of shrinking all text.
- Never label a **reserved site** as an active operational warehouse or say "deliveries available" until a verified route/contract exists.

## Acceptance tests for visual delivery (future review milestone)
1. Screenshots of reserved site, activated warehouse, loaded yard and a real shipment are visually distinct *for a reason*. The labels and illustrations must tell the same truth.
2. At a glance, a player can locate the company site, active vehicle, status and next valid action without reading a paragraph.
3. Trucks fit actual loading bays; road segments join; shadows agree; perspective and scale are consistent across all city scenes.
4. No repeated nonsensical building windows, mismatched text on signs, impossible cargo arrangements, cropped AI artifacts, random floating objects or obviously reused landmark silhouettes pretending to be unique.
5. 60 fps target on supported test devices subject to actual profiling; prefer atlased sprites, pooled moving assets, fixed camera and modest shader work. Do **not** claim performance until Android/iOS measurement.
6. Compare screenshots against authored reference sheets and the real business state; reject assets which are attractive but misleading.

## Pipeline policy
Concept and reference gathering → art direction sheet → 1 real sample depot → animated prototype tied to backend state → device QA → modular expansion to more city archetypes. AI-assisted exploration may support ideation, but final shipped art must be **art-directed, edited, visually consistent and owned/licensed**. No bulk generation and no automatic acceptance of mismatched illustrations. Record asset provenance.

## Current status
START-DEPOT-02 delivers **functional, restrained status hierarchy and backend readiness**, not finished depot scenery. World map art currently includes generic procedural city silhouettes from an earlier draft and needs an authored visual pass before commercial launch. Final compile/Unity/device testing and art review have not been performed yet.

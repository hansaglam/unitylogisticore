# DEPOT-VISUAL-03 — First authored yard asset kit (Unity geometry)

## Delivered in the existing repository
This sprint improves the actual Unity scene-rendered geometry, not just a concept image. It deliberately reuses the existing `DepotYardBlockoutView` and its single mesh/material pipeline. No third-party repository, Unity asset marketplace pack, runtime image generator, external sprite library or extra render dependency is required.

### Art-directed geometry components
- Fixed-perspective operational warehouse: roof standing seams, two static roof vents, consistent light/shade faces and roof-edge trim.
- Three **closed** loading dock doors: dark perimeter seals, segmented metal door panels, dock bumper/guard details.
- Geometrically aligned concrete loading apron, road curb and stationary safety marks.
- Reserved/activation states: four survey markers; prepared pad has concrete expansion joints, with **no working warehouse**.
- All pieces are implemented as authored colored Unity mesh geometry through the existing `Quad` and `Strip` geometry helpers. One mesh and material remain in use; geometry is only rebuilt when the visual stage changes, not per frame.
- Runtime still starts in `Unverified` and only displays a warehouse from a verified `ClientStarterDepotProgressState`; no trucks, moving forklifts or fake pallets.

### Explicit quality boundaries
This is **not final shipping-quality sprite art**. The mesh kit establishes coherent 3/4 projection, proportions, construction materials and readable loading-dock framing, but requires art-direction review in Unity before building a final sprite atlas. Do not claim an artist-authored texture atlas or an asset bundle was produced; this iteration consists of compiled C# procedural mesh data with hand-placed positions.

### Checkpoint
1. In Unity use **LogistiCore → Depot → Create or Open Prototype**.
2. View `Unverified`, `ReservedParcel`, `ActivationReady`, `StorageOperational` and compare alignment, overlays, text contrast and real mobile safe areas.
3. Run `DepotPrototypeVisualTests` including the new per-stage authored geometry assertion.
4. Validate URP 2D shader fallback, draw calls, GC/allocation and 60 fps target *on actual mobile devices*; no performance claims before profiling.
5. Only after review, extract the same authored silhouettes into licensed / internally created editable sprites, maintain an art provenance register and build the final materials/lighting pass. Retain the current blockout as a safe fallback.
6. Do not include `DepotPrototype.unity` in production Build Profiles or enable map-entry navigation until the art, device QA and server-validated state flow are approved.

## No gameplay or release mutations
No Firebase schema, backend call, economy, route logic, warehouse activation, player data, signed-in account migration, map scene or `main` changes.

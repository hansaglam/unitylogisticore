# DEPOT-VISUAL-01 — Authored 2.5D yard blockout (not shipping art)

## Delivered
- A separate fixed-perspective 2.5D depot **blockout** based on explicitly authored mesh polygons (yard pavement, access road, plot outline, closed docks, warehouse roof and walls). No random buildings, AI-generated images, stock visual collages or idle looping traffic.
- Three mutually exclusive, server-driven presentation levels:
  - ReservedParcel: an explicitly verified company-owned site with a marked vacant logistics parcel. No operational warehouse shown.
  - ActivationReady: prepared site footprint, not an operational building; the business is eligible for a separate depot activation action.
  - StorageOperational: an authored small warehouse facade/roof and closed loading docks. Capacity is displayed as 24 pallet slots ONLY if verified. No cargo, inventory pallets or moving forklifts are invented.
- Unverified: neutral ground only, with the explicit UI status that company ownership has not been verified. The editor displays a *design preview* at authoring time, but the runtime resets to Unverified until authenticated company data arrives.
- DepotPrototypeController calls the existing read-only getMyStarterDepotProgress in the strictly approved demo-logisticore-local Firebase emulator, parses the result via the already-existing DTO validator, and derives the visual state through DepotVisualStagePolicy. A change of signed-in Firebase account invalidates the state and clears any implied ownership. There are **no writes or production connections**.
- Minimal company-state-linked TR/EN/DE inspection UI: Parcel, Storage, Dispatch and Back to Map. Dispatch inspection always reports that there is no authorized truck movement or route. No fake earnings, timers, progress or cargo.
- A safe Unity Editor menu entry: **LogistiCore → Depot → Create or Open Prototype**. This generates/open a separate depot scene at Assets/Game/Scenes/DepotPrototype.unity, an orthographic camera, the runtime blockout view/controller and an Input System EventSystem. It does **not** alter the three existing game scenes, Build Profiles, company data, existing map or production asset settings. An existing prototype scene is never overwritten.
- Added Unity .meta GUIDs, a pure state machine and deferred EditMode tests for unverified, legacy, reserved, activation-ready and operational states, including no fabricated vehicles.

## Art direction / design quality
The blockout is *not* a finished professional facility asset. It establishes one coherent isometric projection, proportions, closed dock conventions, road alignment and stage-specific ownership states before a disciplined authored asset-production pass. The **final shipped art must replace the blockout**, using consistent manually art-directed sprites/materials. The fixed geometry can be used as a clear tracing/rigging reference.

See Docs/VisualDirection/LogistiCoreVisualQualityBar.md for the locked visual quality requirements:
- consistent roof/wall/dock perspective and material palette;
- truthful server-state-related construction/storage graphics;
- locale-appropriate architecture/markings based on reference material;
- no fake traffic, pointless procedural particle effects, generic AI-looking skyline duplication or meaningless dashboards;
- no operational-looking warehouse before server activation;
- verify Android/iOS screenshots and performance before shipping.

## What is NOT included
- The new depot scene file is created by the explicit Unity Editor menu, **not automatically committed or added to build**. The repository has the scene authoring and runtime components; the Editor needs to run the scene generator once to create Assets/Game/Scenes/DepotPrototype.unity. Do not claim it exists or is installed in deployed builds yet.
- No production-ready artist-authored sprites, depot asset pack, authored animations, vehicle model, sound, forklift, cargo rendering, loading sequence or warehouse navigation from the world map.
- No remote real-time data synchronization when the backend changes status while the scene remains open; this first milestone reads verified data on entry. Future refresh should use scoped events/reads.
- This is not playable dispatch gameplay or a freight route; ROUTE-01 is a read-only local OSRM preview, and the approved contract/vehicle routing system has not been expanded to Europe.
- No build, PlayMode, EditMode, device or screenshot QA has been run. The prototype must not be published or merged to main until the agreed final verification stage.

## Next
1. Use the Editor generator and inspect fixed scene composition at several Android/iOS aspect ratios, camera zooms and TR/EN/DE labels.
2. Replace blockout geometry with a small coherent hand-authored sprite kit, accurate loading dock scale, architecture and environment variants tied to real supported city context. Keep low draw calls.
3. Add map→verified company site→yard→map navigation only after the scene and art pass meet the final visual quality gate. Do not expose a non-shipping scene in a release build.
4. Add actual vehicle-location and cargo state before parked trucks, forklifts, loading doors or delivery progress animate.
5. Connect HGV routing rules, delivery rights and company economy via ROUTE-02 instead of reusing preview geometry for money.

This is a stacked Draft PR on ROUTE-01 (#35), with no merge/release outside the final quality milestone.

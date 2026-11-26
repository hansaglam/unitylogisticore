# DEPOT-VISUAL-05 — Operational warehouse silhouette correction

## Evidence
Unity Editor screenshot of `DepotPrototype` with `Editor Preview Stage = StorageOperational` showed a wide roof diamond resembling a floating slab, while side walls and loading-bay doors were barely legible. The map/backend connection was not the cause: this was an editor-stage visual review.

## Changes
- Rebuilt the operational building using shared top roof/front wall corners and deeper, visible front/side walls.
- Adjusted roof vs facade material contrast within the existing premium navy/steel palette.
- Repositioned three **closed** sectional loading doors within the front wall face instead of drawing them near the roof edge.
- Grounded the facade with a concrete apron, ground contact strip and static shadow.
- Left Unverified/ReservedParcel/ActivationReady logic and actual Firebase authorization unchanged.
- Added EditMode regression checks for distinct operating facade material colors and their absence in ReservedParcel.

## Verification required
1. In GitHub Desktop switch to `fix/depot-visual-05-building-silhouette`, then open Unity.
2. Exit Play mode. In the depot prototype Hierarchy, select `AuthoredDepotBlockout_EditorOnly`.
3. Choose `Depot Yard Blockout View → Editor Preview Stage → StorageOperational` and inspect **Scene** and **Game** views.
4. Check that the *two walls and three closed docks* are visibly readable and integrated with the road. Photograph the same framing at 16:9 and mobile landscape. If a face still clips, investigate the one-mesh transparent shader draw order before adding textures.
5. Run `DepotPrototypeVisualTests` and the entire Unity EditMode suite. Run the Export Authored Illustration Sprites menu **again** only after visually approving the new geometry; previously exported PNGs become stale when geometry changes.

These changes are source-reviewed only. Unity compile, EditMode, iOS/Android visual/performance tests have not been run. This remains a non-shipping visual prototype; no production Build Profiles or company/economy player records have been modified.

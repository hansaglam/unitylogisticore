# DEPOT-VISUAL-06 — Grounded yard environment and read-only inspection

## Scope of this iteration
The latest Unity screenshot showed the warehouse silhouette corrected, but the paved lot remained two large empty planar slabs. This iteration supplies restrained, deliberately authored yard infrastructure without pretending that freight operations exist.

### Authored environment
- A surveyed parcel now receives consistent fence posts and side rails around three sides, with a front access gap and static threshold marks.
- A few subtle slab/joint lines create readable material scale instead of a giant undifferentiated ground quad.
- Everything is drawn within the existing single mesh/material 2D perspective to avoid extra GameObjects or arbitrary generated structures.
- **Unverified** still shows only neutral road/ground; no fenced site or warehouse appears without a verified player site.
- **ReservedParcel** shows perimeter and surveyed site; **ActivationReady** shows the pad; **StorageOperational** adds the existing improved warehouse and three *closed* bays.
- No parked trucks, cargo, forklifts, traffic activity, moving gates, claimable contracts, or new warehouse capacity.

### Read-only touch/mouse inspection
- A new pure `DepotYardInspectionPolicy.Resolve(stage, point)` classifies authored polygon areas as `Parcel`, `Storage` or `None`. The polygon tests allocate no arrays during pointer taps.
- In the existing **non-shipping** `DepotPrototypeController`, a press on a verified parcel selects the **Parcel** tab; pressing the verified warehouse face/roof selects the **Storage** tab.
- Unity UI hits are ignored via EventSystem. The pointer uses the existing scene camera and the yard's local coordinates.
- No taps are accepted until authenticated local Firebase progress has been verified. **Editor Preview Stage is visual only**; it is not authorization to interact as a real company.
- The Dispatch tab remains informational and does not dispatch anything. No Firebase writes or game economy changes.

### Acceptance gate for Unity
1. Use GitHub Desktop/PowerShell to switch to `feature/depot-visual-06-yard-grounds-and-inspection` after confirming a clean working tree. Open the depot prototype scene in Unity.
2. Exit Play and select `AuthoredDepotBlockout_EditorOnly`; view `ReservedParcel`, `ActivationReady` and `StorageOperational` in **Scene**. Check fence/door overlap, access gap, ground readability and UI framing.
3. Run `DepotPrototypeVisualTests`, including infrastructure and hit-zone tests. Tests are committed but **have not run** in this environment.
4. To test mouse/touch inspection in Play, the exact **local Firebase demo emulator** and an authenticated verified starter depot are required. Without them the current UI must say **Doğrulanmadı** and tapping the static building must not unlock inspection rights.
5. Review TR/EN/DE on landscape Android and iOS aspect ratios, check overdraw, and regenerate the stage PNG illustrations **after** approving geometry because prior exports are stale.
6. Approve final art before shipping. The current geometry is a coherent visual blockout but not textured final professional sprite art.

## Non-goals and safety
- No new repo, third-party pack or generated filler art.
- No map/home/company scene modification, build profile update, Firebase deployment, economy, contracts or data migrations.
- No modifications to `main` or existing published saves.
- Unity compilation, runtime tests, Firebase emulators, phone screenshots and profiling remain unexecuted in this workspace.

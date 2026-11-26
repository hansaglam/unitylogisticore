# DEPOT-VISUAL-09 — Ground plane composition correction

## Evidence
After the DEPOT-VISUAL-08 Inspector-refresh fix, three Unity screenshots correctly show distinct ReservedParcel, ActivationReady and StorageOperational stages. However, the staging revealed the visually dominant flaw: two intersecting, fully exposed rhomboid planes looking like floating platforms. The lot edge and road did not read as a coherent logistics yard.

## Implemented
- Replace the two large overlapping ground/road silhouettes with one authored site ground quad and one *inset* public access-road quad.
- Keep the access road geometrically inside the ground silhouette; use a thin curb and limited lane marks rather than a second exposed platform.
- Preserve the warehouse geometry from DEPOT-VISUAL-05, verified infrastructure from DEPOT-VISUAL-06, visual stage refresh from DEPOT-VISUAL-08 and read-only inspection policy.
- Add a Unity EditMode contract checking that all four approach-road vertices lie within the four-corner ground polygon. Also verify Unverified has no company-owned facility, vehicle or infrastructure.

## Validation required before acceptance
1. With Unity closed and a clean Git worktree, switch to `fix/depot-visual-09-ground-plane-composition`.
2. Open the existing locally-created `DepotPrototype.unity` scene, preserving its stash or committed backup. Do not run git clean/reset or discard the scene.
3. In Edit Mode select `AuthoredDepotBlockout_EditorOnly`, switch the Inspector stage through ReservedParcel, ActivationReady, StorageOperational and capture Game view at the same 16:9 framing.
4. Confirm road is contained within one site silhouette, warehouse feet do not float, prepared slab appears only at ActivationReady, and no warehouse is visible at ReservedParcel.
5. Run `DepotPrototypeVisualTests` in Unity Test Runner; run the entire EditMode suite before considering merge. Unity/device validation has **not** been run in this environment.
6. Re-export stage PNGs only after the geometry is visually accepted. Existing exported images are now stale if they were generated before this branch.

## Limitations
This is still a non-shipping 2D mesh blockout; professional authored textures, selective sprite atlas, art direction, Android/iOS mobile QA and real Firebase verification are outstanding. No map, economy, player data, build profile or `main` mutation.

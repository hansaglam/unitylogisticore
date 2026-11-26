# DEPOT-VISUAL-07 — Yard composition and entry visual hierarchy

## Why
Recent Unity screenshot showed two vast overlapping gray rhomboids below the now-readable warehouse. Even after DEPOT-VISUAL-06 added fencing, the approach-to-site transition remained visually unclear and the authored environment looked like separate floating platforms.

## Implementation
- Rebalance the fixed hand-placed perspective road and the tarmac: one approach-road shape, one bounded tarmac field and thin edge/shoulder strips.
- Replace scattered road lines with a restrained set of aligned lane marks.
- Add final-pass, stage-gated site entry wayfinding: one entrance threshold and three small paint marks, overlaid after other static geometry to stay visible.
- Do not add parked trucks, dynamic gates, clients, contracts, money, delivery permissions, fake stock or new company assets.
- No new Unity scene, renderer components, external asset packs, dependencies or generated noise: same existing mesh and fixed color palette.
- Keep operational building roof/facades and loading docks unchanged.

## Verification and local worktree
1. The previously generated `DepotPrototype.unity` scene is **local user content** stored in their Git stash. This PR does not include it. Preserve it when switching branches, and restore after inspecting the stash; do NOT use `git stash pop` blindly.
2. If the safe updater reports local changes, stop. Use a backup or explicit reviewed stash before checkout.
3. In Unity inspect the same camera/framing in Unverified, ReservedParcel, ActivationReady and StorageOperational. Compare road/yard alignment, the entrance threshold, fence and loading apron.
4. Run Unity EditMode tests, including `EntryWayfindingOnlyExistsForVerifiedSiteStages`.
5. The project has not been compiled/tested in this environment. These new planar surfaces are a *geometry iteration*, not proof of final professional textures or sprite art.
6. The generated stage PNG illustrations should only be re-exported *after* the geometry is visually approved.

## Release safeguards
No production Firebase, no change to `main`, no Build Profiles, player account migration, dispatch, route, economy or save-data mutations. The depot remains an editor-only prototype.

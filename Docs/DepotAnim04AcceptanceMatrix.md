# DEPOT-ANIM-04 — Deterministic motion acceptance matrix

## Delivered code
The existing `DepotMotionRig` now exposes `Advance(float deltaTime)`, used by its normal Unity Update loop and by deterministic tests. The function does not read any backend state or generate events. The step ignores NaN, Infinity, zero and negative deltas, and moves individually authored gate/dock leaves only after accepted verified evidence.

Two new source-level EditMode tests check:
- closed rest state does not move; a verified operational event advances the separately bound gate and dock to independent endpoints;
- a confirmed close returns both to their original rest poses, and losing verification snaps both shut even mid-animation;
- invalid frame deltas cannot move transforms.

These tests are written but not executed by the GitHub connector. A clean compile or passing Unity run must still be provided from the local Editor.

## Production art status and blockers
The existing ART-02B and ART-02C local commits have NOT been pushed to GitHub. The remote `feature/depot-art-02-production-sprite-kit` still points to `8265d9e`; GitHub reports no commit object for local ART-02C SHA `1ea0ff8820dfbc393e4bc4254a6a3c4c1578000c`. This is a hard block for remote continuation of the actual source PNG artwork. The first asset sheet is available in conversation uploads but the more accurate *local* Codex-generated .meta, scene and production-spec state cannot be inspected via GitHub.

The 16 PNGs are not yet production approved. Prior ART-02C QA called out re-authoring the perspective-inconsistent sliding gate, translucent fence and orthogonal corner, preserving the detailed warehouse as the projection reference rather than corrupting it. A professional gate assembly requires a separately authored static frame/pillars, a movable leaf, real alpha openings, and a documented ground-contact baseline. A dock animation requires a shutter leaf separate from the existing warehouse front, not moving/cropping the entire baked PNG.

## Integration checklist for ART-03

| Category | Acceptance |
|---|---|
| Art | Approved layered gate, straight/orthogonal fences, corner and matching post material/scale with alpha cutouts |
| Unity | Import as single sprites with checked pivots, .meta, source provenance and texture compression for Android/iOS |
| Layout | At least one calibrated camera 16:9 composition and small-scale 800×450 screenshot with no seams |
| Motion | Gate opens without shifting fixed posts, closed position preserved, dock independent |
| Authority | Unverified player sees no site; no event means no motion; session reset snaps both leaves shut |
| Regression | EditMode tests all pass against prior DEPOT baseline; unrelated 58 historical failures reported separately |
| Runtime | PlayMode on an isolated scene confirms interpolations with temporary explicit test evidence only |
| Devices | Android and iOS screenshots, overdraw, performance, memory; no shipping until checked |
| Ops | Real server-confirmed event identity/expiry contract required before connecting live motion |
| Shipping | Only after asset and event/data reviews may a production scene or Build Profile change be proposed |

## Worktree preservation
Keep the user's unpushed ART-02C branch `D:/Projects/unitylogisticore-depot-art-02` untouched by these GitHub-only changes. Do not reset/clean it or drop its stash/history. After Unity is closed and that worktree is clean, an ordinary **non-force** push of `feature/depot-art-02-production-sprite-kit` will synchronize the local 16 PNG files and provenance, after which branches can be reconciled without losing work.

## Explicit boundaries
No shipping sprites or completed source PNG re-authoring claimed. No Unity compile, EditMode, PlayMode or phone tests were run for ANIM-04. No live scene wiring, backend call, Firebase operation, route, economy, player save, Build Profile or `main` change.

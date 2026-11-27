# DEPOT-ART-03 — Stage-specific production sprite layout

## Implementation in this branch
The verified depot presentation assembly now has four independent serialized renderer collections:
- `siteRenderers`: verified parcel/site boundary and neutral infrastructure, shown in ReservedParcel, ActivationReady and StorageOperational.
- `activationRenderers`: construction/foundation art, shown ONLY in ActivationReady. Hides automatically after warehouse completion.
- `warehouseRenderers`: the approved building and static dock structure, shown ONLY in StorageOperational.
- `operationalDetails`: independent operational-only noninteractive visual details, shown ONLY in StorageOperational.

Any unverified read, invalid stage, scene disable or sign-out must hide every company-owned collection and clear `DepotMotionRig` movement targets. `DepotMotionPolicy` independently refuses gate and dock movement unless an authenticated operational state and event flags are both supplied. No scene is connected to live backend events yet.

## Review-only hierarchy plan
```text
DEPOT_ART_03_REVIEW_ONLY
  Ground_Public                 (neutral, independent of ownership)
  Site_Verified                 (siteRenderers)
    FenceSegments               (correct common camera required)
    EntrancePillars
    Wayfinding
  ActivationReady_Foundation    (activationRenderers)
  Operational_Warehouse         (warehouseRenderers)
    Building_Static
    DockStructures
  Operational_Details           (operationalDetails)
  MotionRig                     (not active without explicit verified event source)
    Gate_StaticFrame            (fixed, NOT the moving leaf)
    Gate_Leaf                   (movable separate art, v2 required)
    DockDoor_Leaf               (movable separate art, separately authored)
```

## Asset authoring acceptance checklist
1. Warehouse establishes approximate projection but is not perfect isometric: use the locally authored ART-02C ProductionSpecification and calibrated Unity camera.
2. New `fence_straight_v2`, `fence_corner_v2`, `sliding_gate_leaf_v2` must be separate genuine RGBA assets; do not use old flattened gate/fence as approved modular parts.
3. Gate posts/frame are stationary; only the gate leaf travels. Do not animate the entire flat gate PNG, its shadow, or gate posts.
4. Warehouse rolling shutters are moving **only if** separately drawn door layers can be substituted cleanly; otherwise keep them closed and static.
5. Keep authored dimensions, consistent contact anchors, unmirrored orientation, uniform scaling and common light/shadow direction. Review full alpha on dark and light grounds, and legibility at 800×450.
6. Every sprite has import settings, .meta, source provenance, physical size, pivot, contact and render order recorded. The same scene must work in 16:9 as well as target device aspect ratios.
7. No verified site means no site fence/warehouse. No server-confirmed event means no gate/door action or fabricated vehicles.
8. Run Unity Editor compilation, DEPOT EditMode, PlayMode scene movement verification and phone performance capture before approving any production asset.

## Repo and safety status
The remote `feature/depot-art-02-production-sprite-kit` branch is still at `8265d9e` and does not include the user's unpushed local ART-02B/C commits or actual PNGs. **This branch therefore cannot claim to integrate or visually approve final assets.** Work here is source/test preparation, intentionally separate from the local Codex worktree. Do not merge this draft or shipping scenes into main until assets and device checks land. No Firebase, economy, map, Build Profiles or saves were changed.

## Current test status
New Unity EditMode regression covers ReservedParcel → ActivationReady → StorageOperational → Unverified transitions and mutually exclusive foundation/warehouse artwork. Test source committed; Unity runner not available through the GitHub connector, so result remains **unverified**.

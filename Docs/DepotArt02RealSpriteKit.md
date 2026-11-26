# DEPOT-ART-02 — Real 2D sprite candidate kit and Unity review pipeline

## Assets produced
16 unique, transparent PNG sprite candidates from one consistently art-directed isometric industrial set: 3-bay warehouse; security booth; straight/corner fences; gate pillars (light, CCTV); sliding gate; blank company sign; direction arrow; straight/corner curbs; two landscaped planters; two bollards; loading-dock bumper.

**Important delivery boundary:** PNG binaries are supplied separately in `LogistiCore_Depot_Art_02_Kit.zip` in the same ChatGPT conversation. They are **not embedded in this GitHub PR** until imported and committed. The PR contains the verified filenames, Unity importer and a self-contained art-review scene generator. Do not claim final asset integration before reviewing the actual PNGs in Unity.

## Exact installation
1. Download the ZIP supplied by ChatGPT and extract it locally.
2. Copy the enclosed `Assets/Game/Art/Depot/ProductionCandidates/` directory under the same relative path in the Unity repository; keep all 16 PNGs together.
3. Re-open Unity and run **LogistiCore → Depot → Art 02 → Import 16 Sprite Assets**. Each asset is set to Sprite (2D and UI), single, custom bottom-center pivot, alpha transparency, no mipmaps, bilinear, uncompressed, 100 PPU.
4. Then run **LogistiCore → Depot → Art 02 → Create Art Review Scene**. This creates `Assets/Game/Scenes/DepotArtReview.unity`, separate from the live game. It asks before overwriting a manually edited review scene and preserves unsaved scene changes through Unity's save prompt.
5. In the review scene, inspect alpha fringes, perspective consistency, daylight/shadow direction, image-to-image scale, oversaturated or implausible details and mobile landscape legibility. Unity may need manual tweaks to sprite positions and scales; these are preliminary review-layout values.
6. Run EditMode tests and inspect any import/compile errors. Save reviewed `*.png`, `*.meta` and any approved scene asset as a **separate Codex/Unity Git commit** on this feature branch. Do not add `DepotArtReview.unity` to a release Build Profile before QA.
7. Only when both scene screenshot and review pass are approved, implement stage-gated SpriteRenderer prefabs in the *real* depot scene and remove only those overlapping temporary mesh shapes. The existing `DepotVisualStagePolicy` remains the authoritative permission gate.

## Asset-stage restrictions
- **Unverified**: no owned warehouse, fences or gate/booth should be displayed to the player.
- **ReservedParcel or higher**: surveyed-site environmental components may be shown only when company/site state is verified.
- **StorageOperational**: the warehouse and dock bumper may appear only after a verified operational warehouse state.
- No moving trucks, loaded cargo, fictitious workers, operational dispatch rights, money or contract permissions are fabricated.

## Scope and provenance
- Original generated set for LogistiCore. It is not third-party licensed asset-pack content. Nevertheless the PNGs are **production candidates**, requiring manual review and production acceptance.
- No live Firebase, saved economy, player identity, dispatch backend, existing scene or release profile changes.
- Unity Editor compile, Android/iOS visual QA, art evaluation and performance profiling were not executed through the GitHub authoring connection.
- Current world map + depot prototype are source code and stage-policy baselines; do not silently overwrite them with candidate sprites.

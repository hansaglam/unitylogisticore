# DEPOT-VISUAL-04 — Authored sprite illustration export

## What is implemented
- Unity Editor menu **LogistiCore → Depot → Export Authored Illustration Sprites** captures the already-authored `DepotYardBlockoutView` into four deterministic **1024×768 transparent PNG** illustrations.
- The output stage names are defined in `DepotIllustrationCatalog`: `depot_neutral.png`, `depot_reserved.png`, `depot_prepared.png`, `depot_operational.png`.
- A Unity **PreviewScene** isolates the renderer from player scenes and editor camera state; a temporary orthographic camera captures only the authored yard view.
- PNGs are imported as single sprites with transparent alpha, bilinear filtering and no mipmaps. No third-party repo/package is needed.
- The exporter requires an explicit editor click and warns before replacing those four named output PNGs.
- Added a regression test locking stage filenames and preventing extra/unknown stage art.

## Output is not committed yet
The exporter source is committed. **The four binary PNGs are NOT yet in GitHub** because the Unity Editor has not run here. Run the menu command locally, inspect the four sprites under `Assets/Game/Art/Depot/Generated`, and commit reviewed PNGs and their Unity `.meta` files separately after Unity import.

## Quality and gameplay boundaries
- This produces **stage illustration sprites**, not a layered character/vehicle atlas or a commercially approved hand-painted art set. The original mesh remains the interactive scene renderer and safe fallback.
- It does not draw moving vehicles, loaded palettes, dock animation or fake freight. An operational warehouse illustration remains a static art reference; gameplay only displays the owned depot when `DepotVisualStagePolicy` accepts server state.
- The exporter does not modify `main`, any Build Profile, existing scenes, Firestore collections, economy, company progress or route authorization.
- Next: review camera framing/transparency and pixel edges in Unity, perform actual EditMode + Android/iOS visuals, then replace or polish the exported sheets with licensed/authored art before shipping.

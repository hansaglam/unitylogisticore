# LogistiCore — VISUAL-01 / Premium Türkiye Map (first vertical slice)

## Status
**Draft / Unity verification required.** Branch: `feature/visual-01-turkey-map`, stacked upon `feature/ui-redesign-02` (which itself depends on UI-01). This is a focused cartographic and selection-presentation change, **not** final reference-art parity.

## Goal
Move the first playable map toward a premium, readable 2D strategy presentation without altering the protected scene or inventing backend state.

The screenshots supplied by the owner showed a largely empty geographic surface, basic eight-city dots and a minimal interface. This slice provides a visibly readable city connectivity layer on the **Türkiye** scope, subdued connection glow and selected-city context. It deliberately does not add fake trucks, claimed delivery routes or non-existent contract rewards.

## Architecture
- `MapSchematicLinks.cs` contains eleven explicitly authored *visual links* among the eight city coordinates already in `MapCities`. Each link's endpoints come from `MapProjection`; intermediate points are gently curved, illustrative lines.
- `MapPremiumNetworkOverlay.cs` is a runtime-only view of those links. It creates cached `LineRenderer` objects and 8 city halo rings, using a single generated material and reusing line vertex buffers.
- `MapMarkerLayer.OnEnable()` attaches the overlay when playing, while the already-existing city nameplates receive cached 9-slice rounded surfaces. It does not change the scene, prefab, serialized marker arrays, existing hit areas, translation bindings or the city-selection signal.
- Overlay is shown only in `MapScope.Turkey` when `MapMarkerLod.AtCountryScale` already shows individual city markers; it remains hidden for aggregated country badges, the world and other continents. Zoom changes keep approximate screen-pixel stroke widths and halo sizes stable.
- Selecting a city highlights only nearby graph edges and its halo, dimming unrelated links. This is a *visual neighborhood*, not an active delivery or server route.
- The overlay is noninteractive and owns no raycast targets.
- Generated runtime objects and material are disposed with the scene; no per-frame texture, GameObject or path-array creation.
- `MapSchematicLinksTests.cs` verifies city IDs, unique graph edges, connectedness, coordinate projection, deterministic path endpoints and adjacency.

## Important truthfulness boundary
**Links are NOT road geometry** and must never be used as actual travel distances, movement simulation, available jobs, ownership, contracts or live market connections. They are only cartographic cues. Product UI that depicts a real planned/active route must use actual routing and backend data, visually distinguished from these schematic lines.

No made-up vehicles or server values are inserted. Offline mode still correctly reports no active connection.

## Visual settings
- Base cyan connection strokes: ~1.55 screen pixels at 38% opacity plus 5px faint glow.
- Selected-city incident links: ~2.8px at 93% opacity plus 9px controlled glow.
- City halos: ~19px radius default, ~25px selected, camera-relative.
- Hidden at world scale and if camera/regional context is unavailable.
- Shared runtime shader material; shader lookup must be checked on Android/iOS player builds (potential shader-stripping risk).
- Existing world/map relief and marker sprites stay unchanged.

## Unity verification gate — DO NOT MERGE YET
1. Unity 6000.3.23f1: zero **new** compile exceptions and runtime warnings; test sprite shader availability.
2. EditMode: run `MapSchematicLinksTests` (5), `MapPremiumHeaderRailTests` (4), `MapPremiumUiTests` (5), `MapMarkerLodTests`, `MapChromeLayoutTests`, `MapRailTouchTargetTests`, `MapTopHudTests` and map scene-safety coverage.
3. Game View: choose **Türkiye scope** at 1920x1080 and 844x390. Screenshot full Turkey view with no city selected, then click Ankara; confirm only adjacent visual links light up. Zoom to World: overlay must disappear. Switch to Europe/Asia: overlay must disappear.
4. Test the zoom and left-rail scrolling without graph blocking hit targets. Verify status panel and Jobs/Fleet open, city labels stay readable and map retains enough available space.
5. If the overlay fails to draw, inspect Console for shader warning. Do not change protected Graphics/ProjectSettings automatically.
6. EN/TR/DE: existing localized city names must stay correct; new overlay introduces no new copy.
7. Android/iOS build: run an actual player test before claiming device performance or shader support.

## Known limitations and follow-up
- The schematic network does not yet match actual highway geometry; a real road network will require properly sourced transport GIS data and carefully documented routing semantics.
- Route animations, trucks, active delivery overlays, rich city detail cards and premium atlas asset work are later tasks. None should display false backend state.
- Existing `ProjectSettings.asset` test-hash mismatch and prior `ResourceManager.Update / WaitForCompletion` error remain separate tracked issues.
- PR #19 (UI-01) and #20 (UI-02) are still draft and earlier regression results are incomplete.

### Next art/product gate
Assess screenshots next to the owner's four reference images. If the first Turkey scene still looks too sparse, iterate map aesthetics and camera composition before expanding to market or online gameplay UI.

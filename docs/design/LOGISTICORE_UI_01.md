# LogistiCore — UI-REDESIGN-00/01

## Intent
Design direction is **Balanced Premium Mobile Logistics Strategy**, guided by the four reference images supplied by the owner. A refined navy/cyan atmosphere is the target, but avoid an always-on dense desktop HUD on compact landscape devices. The interactive map remains the primary gameplay surface.

This PR delivers the **first UI foundation**, **not** the complete reference-image design. UI-02 will recompose the top HUD and left rail; UI-03 will address map interactions; UI-04 will rebuild Jobs/Fleet/Warehouse/Market/Road panels.

## Architectural boundaries
- Keep `WorldMapVerticalSlice.unity`, `ProjectSettings`, package versions and Firebase backend untouched.
- Unity UI is presentation only. Backend is authoritative for money, XP, deliveries, contracts and persistent state.
- Never manufacture balance, reputation, XP thresholds, weather, notifications or delivery status for screenshots.
- No authorization flag changes or live Functions commands.
- Retain EN/TR/DE localization, reduced motion direction, safe area and existing navigation.
- Existing map layout, touch targets, camera and world overlays stay in place.

## Source files
- `Assets/Game/Map/MapPremiumTheme.cs`: design colors, semantic roles, typography and contrast helper.
- `Assets/Game/Map/MapPremiumSprites.cs`: cached procedural 48px 9-slice rounded white sprite, tintable by uGUI.
- `Assets/Game/Map/MapPremiumUi.cs`: reuse across display surfaces, buttons, text and status chips; no game state.
- `Assets/Game/Map/MapHudPalette.cs`: compatibility facade so existing modules use the same palette.
- `Assets/Game/Map/MapCommandShell.cs`: common background and separator color tokens.
- `Assets/Game/Map/MapManagementPanel.cs`: rounded management panel and selection menu surfaces.
- `Assets/Game/Map/MapTopHud.cs`: rounded HUD cluster surfaces.
- `Assets/Game/Localization/OpsContent.cs`: five missing HUD constants; strings already existed in EN/TR/DE tables.
- `Assets/Game/Tests/MapPremiumUiTests.cs`: source-level Unity test coverage for primitives.

## Palette
| Token | HEX | Meaning |
| --- | --- | --- |
| Background | #071522 | Navigation/map chrome |
| Panel | #0E2638 | Panel surface, 0.96 opacity |
| Card | #17384E | Elevated surface, 0.94 opacity |
| Button | #12364A | Default control |
| Selected | #14516C | Active control |
| Cyan | #18BCEB | Focus, information |
| Success | #16D8A1 | Positive verified status |
| Warning | #F4B860 | Needs attention |
| Error | #F07178 | Failure |
| TextPrimary | #E9F2FA | Main text |
| TextSecondary | #AFCCDB | Support text |

Font roles: title 18, body 15, caption 12 (logical design units). Actual displayed sizes and contrast must be evaluated at each supported resolution with real map textures beneath translucent UI.

## New component contracts
- `MapPremiumUi.StyleSurface`: styles an existing image without moving its hierarchy. Caller chooses raycast behavior.
- `CreateSurface`: builds a non-interactive image that does not intercept map touches.
- `CreateButton`: creates a visual button; owner MUST connect a genuine action and proper localized label.
- `EnsureTouchTarget`: sets LayoutElement minimum hit size based on `MapCommandLayout.TouchPixels` and caller's Canvas scale.
- `CreateText`: receives text from the caller; does not consult Localization/Addressables in callbacks.
- `CreateStatusChip` / `SetStatusChip`: display caller-supplied localization and semantic severity.
- `MapPremiumSprites.Rounded`: lazy/cached procedural 9-slice; no per-frame generation.

A `LayoutElement` only sets constraints for compatible layout groups. When using manual RectTransform positioning, the caller must also guarantee actual hit bounds.

## Tests and gate before merge
1. GitHub/source checks: no backend, scene, ProjectSettings or package modifications.
2. In Unity 6000.3.23f1, compile and run `MapPremiumUiTests`, `MapTopHudTests`, `MapChromeLayoutTests`, `MapRailTouchTargetTests`.
3. Run relevant PlayMode smoke checks and review the Console: zero compilation errors and zero new runtime errors.
4. Validate 844x390, 932x430, 1280x720, 1920x1080, 2868x1320 safe-area cases, then TR/EN/DE.
5. Take `WorldMapVerticalSlice.unity` Game View screenshots; ensure legibility, no overlapping HUD, and map remains usable.
6. Do not declare 60 FPS achieved without real device evidence.

**Known pre-existing issues:** `MapTopHudTests` pins an outdated SHA for `ProjectSettings.asset` relative to the a36b01e commit. Investigate rather than blindly updating the expected hash. A prior `ResourceManager.Update` / `WaitForCompletion` runtime exception needs its full stack trace and a separately scoped fix; no package version change is authorized here.

**Important local working tree precaution:** the owner already modified `OpsContent.cs` locally to add the five HUD fields. The same edit is now in the feature branch. Preserve that local edit safely before checkout and do not blindly re-apply a stash over this branch.

## Next deliverable — UI-02
Build new top HUD and compact rail on these tokens, retaining the real-data/offline gates. Then deliver one complete Jobs panel vertical slice before applying the same UI system to all modules.

## Explicit non-goals
No real-time multiplayer, illegal-economy features, industry expansion, new route simulation or production Firebase changes in UI-01.

# LogistiCore — UI-REDESIGN-02

## Status
Implementation branch: `feature/ui-redesign-02`. Stacked on `feature/ui-redesign-01` (PR #19), **not** on `main`. UI-01's five `MapPremiumUiTests` were reported PASS from Unity by the owner; the old `MapTopHudTests`, `MapChromeLayoutTests`, and `MapRailTouchTargetTests` have not yet been reported for this updated branch. **Do not merge until the Unity regression gate is complete.**

## Intended visual change
Balanced premium mobile 2D style, not a dense desktop dashboard:
- Keep the existing LogistiCore letterform and its chip clearance but introduce a distinct procedural blue cube insignia.
- Give the header and module rail true 9-slice surface styling, subtle top cyan rule and persistent dark-layer separation.
- Each rail item gets an independently drawn 9-slice face, icon backing plate and 3px selected-state indicator. A selected module gets a cyan outline; unavailable transport modes remain disabled and visually muted.
- The trusted HUD uses a compact vertical accent: success = **trusted balance available**, cyan = **read in flight**, warning = **data unavailable**. It does *not* signal server online/offline state.
- Avoid inventing XP progress, reputation, weather, time, alerts, fleet or contracts.
- No new Unity prefabs or scene modifications, no gameplay changes, no backend writes.

## Implementation details
- `MapPremiumBrandIcon.cs`: cached 64×64 2D cube sprite with three polygonal facets, no external art file or runtime network dependency.
- `MapCommandShell.cs`: creates a non-raycast `BrandEmblem` and `HeaderTopAccent`, preserves "Logisti"/"Core" objects, reserves additional space before `RegionChip`, installs noninteractive layered rail backgrounds and active-state accents without modifying the existing rail row geometry or selection events.
- `MapTopHud.cs`: nonblocking `BalanceStateAccent` child changes color according to *trusted economy snapshot readiness*, not speculative company values.
- `MapPremiumHeaderRailTests.cs`: four EditMode checks for cached brand asset, decorations that do not steal hits, active rail feedback and compact brand-chip nonoverlap.

## Technical constraints
- `WorldMapVerticalSlice.unity`, `ProjectSettings`, Localization assets, `Packages`, and Firebase backend remain untouched in this UI-02 diff.
- Existing `MapCommandLayout.RailFrames`, scroll and touch hitboxes remain unchanged.
- Rail state colors update on existing `PaintRows` calls; no custom per-frame updater.
- Top HUD still only shows trusted data. Localized copy stays EN/TR/DE.
- `BrandEmblem` is actual pixel art; `BrandMark`, `HudNotify` and `HudClock` synthetic chrome remain absent.

## Required Unity checks
1. Ensure UI-01 changes are present. Checkout branch safely; local dirty files must be preserved and should not be blindly overwritten.
2. Compile in Unity 6.3.23f1: zero new compiler errors.
3. EditMode: `MapPremiumHeaderRailTests` (4), `MapPremiumUiTests` (5), `MapTopHudTests`, `MapChromeLayoutTests`, `MapRailTouchTargetTests`. Record PASS/FAIL/SKIP with console logs.
4. Test Game View: 844×390, 932×430, 1280×720, 1920×1080, 2868×1320 with a safe-area case; test TR, EN and DE.
5. Select Jobs/Fleet and confirm cyan rail accent, no double action, close/reopen preservation, no marker/rail hit stealing, no header overlap with region chip.
6. Check read-only/offline state; no made-up economy numbers, and no new runtime `ResourceManager.Update` exception.
7. Get real device screenshots before calling the redesign polished. UI-02 is a chrome update; rich map/route effects and management content are intentionally deferred to UI-03/04.

## Known separate issues
- The prior `MapTopHudTests.ProtectedFiles_UnchangedAndNoFakeChrome` uses a `ProjectSettings.asset` SHA that may be stale. Confirm actual file lineage before changing that assertion; do not auto-update hash to conceal unexpected file mutations.
- A previous Unity Addressables `WaitForCompletion` runtime error was observed. Do not interpret a clean source diff as proof it is fixed.

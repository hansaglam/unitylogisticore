# LogistiCore VISUAL-02 — City experience and atlas polish

**Status:** DRAFT, Unity compile and visual QA not yet run. This feature branch is stacked on `feature/visual-01-turkey-map` (Draft PR #21), which is stacked on UI-02 and UI-01. Do not merge independently into `main`.

## Design direction
The user's approved four concept references are the quality bar: 2D premium navy/cyan strategy game with readable mobile UI, a live-feeling—but truthful—map, and an expressive selected-city panel. This iteration makes the map selection and city context more satisfying without inventing gameplay.

## Visible changes
- `MapPremiumCityArt.cs` procedurally generates a single shared **generic stylized skyline** (320×128) with navy/teal lighting. It depicts no specific real landmark and requires no external image dependency.
- `MapCommandShell.cs` reuses the existing `ContextPanel`: 9-slice frame, landscape skyline hero, raised coordinates card and **schematic network** card with a count derived from `MapSchematicLinks.NeighborCount(cityId)`. The existing city label and real geocoordinates remain the source of truth. Module / no-city contexts do not show the city graphics. All new graphics are non-raycast.
- `OpsContent.cs` and the shared Ops / EN/TR/DE tables include two new localized labels: `ops.map.schematic.title` and `ops.map.schematic.note`. The note states that these are visual connections rather than active routes.
- `MapPremiumNetworkOverlay.cs` creates small noninteractive junction dots on the schematic graph. Selecting a city smoothly fades in only its incident schematic links and briefly expands its ring (~0.28 seconds). Nonselected links recede. A component-level `reduceMotion` toggle disables the reveal and applies the final state immediately.
- `MapSchematicLinks.cs` adds the deterministic `NeighborCount` helper; not a route, delivery, distance or economy API.
- `MapPremiumPanelArt.cs` adds a shared abstract management-console gradient/grid header texture (256×88). The existing `MapManagementPanel` now has this header art, a color accent that distinguishes module sections, and consistent button, module-picker and close/expand surfaces. All five management modules also use matching 9-slice list rows and premium selected-state colors, with the original touch targets and data logic unchanged.

## Non-negotiable truth boundaries
- No active delivery, truck, fleet, contract, invented route, dispatch time or server economy state has been created.
- The schematic graph is purely presentational. Any future *real road network* must be sourced separately and never derived from this visual graph.
- Offline mode keeps its actual unavailable state.
- No new `Canvas`, `EventSystem`, `Scene` changes, `ProjectSettings`, package version, Firebase backend action or live-function authorization.

## Implementation safeguards
- Static generated skyline sprite and map line material are cached, rather than rebuilt every Update.
- The selection transition runs only while the 0.28s state change is in progress. Pan/zoom changes retain the existing viewport update scheme.
- Route strokes and rings scale with the current ortho size to maintain stable screen appearance.
- In the city panel, the text is still supplied through existing Localization bindings; there are no hard-coded player-visible English-only labels.
- New art/cards never intercept mouse/touch input.
- City data still comes from the eight existing authored `MapCities` coordinates.

## Test gate after this visual batch
1. Unity 6000.3.23f1 compile: zero new compilation errors. Do not claim a PASS from GitHub-only review.
2. EditMode: `MapPremiumCityExperienceTests` (7), `MapSchematicLinksTests` (5), plus the existing `MapPremiumHeaderRailTests`, `MapPremiumUiTests`, `MapMarkerLodTests`, `MapCommandShellTests`, `MapChromeLayoutTests`, `MapRailTouchTargetTests` and `MapTopHudTests`.
3. Game View Türkiye, default, and city-click: tap İstanbul, Ankara and Adana and check that schematic link count changes, title/coordinate display localizes and related map lines animate.
4. Screen sizes: 844×390, 932×430, 1280×720, 1920×1080 and 2868×1320 Safe Area; ensure the city cards fit without clipping or obscuring the close button.
5. Switch TR/EN/DE; verify both new strings, existing coordinate decimal formatting and display names.
6. World/Europe zoom: no Turkish city graph overlay; back to Türkiye: overlay should return.
7. Open/close Jobs/Fleet/Warehouse/Market/Road panels and confirm the shared header art, list cards and controls retain taps, selection behavior, scroll position and real data while never substituting fake gameplay. Scroll left rail and pinch/drag map: all original hitboxes remain intact.
8. Inspect Console for `ResourceManager.Update / WaitForCompletion` from previous investigation.
9. Android and iOS player preview for shader stripping, touches, texture memory, frame time and appearance. No device performance claims before a build.
10. Compare actual captures to reference concept art; **this batch is still not reference-image parity**.

## Further visual work after real captures
- Fine tune map background illumination and line complexity. Do not add noise or fake active trucks just to fill space.
- Build richly structured Jobs and Fleet panels using the same shared UI tokens, once backed by actual player state.
- A real transport GIS network and verified operations animation remain separate gameplay/data work.

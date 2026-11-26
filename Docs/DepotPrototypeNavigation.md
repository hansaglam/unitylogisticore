# DEPOT-VISUAL-02B — Guarded prototype scene route

## Scope
- `DepotPrototypeNavigation.CanInspect` is a pure gate: requires authenticated local read, verified parsed starter-depot response, existing company/HQ/reserved site, matching country-scoped HQ city, a catalog-known city, and an explicitly registered prototype scene.
- `TryOpen` additionally checks the existing local Firebase emulator read guard and installed scene. It is compiled as **editor-only navigation**; Android/iOS player builds return false even if a prototype scene is accidentally included.
- `AppNavigation` retains its existing Home/Company/WorldMap behavior. No build profiles, serialized scenes, Firebase rules, account records, warehouse rights or inventory are modified.
- EditMode test contracts cover missing company/HQ/site, mismatched city, missing emulator/verified state, unregistered scene and unknown cities.

## Required integration after the art/QA gate
1. Generate the scene with `LogistiCore → Depot → Create or Open Prototype`, review the art composition and locale text on representative mobile aspect ratios.
2. For editor testing only, register the prototype scene in the local Editor's scene list. Do not change the release Build Profiles.
3. Only after an explicit user action and a *fresh authenticated* `getMyStarterDepotProgress` response from the approved local emulator, pass the parsed response and selected city to `DepotPrototypeNavigation.TryOpen`.
4. Avoid persisting the authorization flag or trusting a local cache. Re-read state after identity changes.
5. Return using the existing `AppNavigation.ShowMap`. No map button is wired by this milestone; this work adds the fail-closed navigation contract, not an exposed player feature.

## Limits
- The scene is a 2.5D blockout, not final authored art or a playable logistics facility.
- No production-grade map→depot navigation until the blockout is replaced and platform visual QA passes.
- No trucks, routing authorization, dispatch, payouts or warehouse mutation is implemented.
- Unity compile, EditMode, emulator, device and visual checks are not executed in this environment.

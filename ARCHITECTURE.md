# LogistiCore foundation

Unity 6.3 Universal 2D client. This document covers UNITY-00 only.

## Ownership

The client draws, animates, caches reads, and sends commands. The backend validates and decides.

The client will own presentation, input, camera, animation, interpolation, read caching, the language override, and display formatting.

The backend will own money, XP, progression, contracts, deliveries, assets, market, economy, events, and persistent state. Firebase Auth, Firestore, and authoritative functions are not part of this phase. No backend interface was added, because nothing here calls a backend.

Domain values stay numeric. Display code formats them. Currency is an explicit ISO code and is never converted from the locale.

## Layout

`LogistiCore.Runtime` holds the playable code. `LogistiCore.Editor` holds the one-shot setup menu. `LogistiCore.Tests` holds edit-mode tests.

There is no game manager, singleton service, or dependency container. `RuntimeFoundation` applies the frame-rate target once before the first scene.

## Localization

English (`en`) is the master language. Turkish (`tr`) and German (`de`) are the other V1 languages. Locales are language codes, not regions, so a device using `en-GB` or `de-AT` still matches.

Startup selectors, in order:

1. Command line `-language=` for a QA override.
2. PlayerPrefs key `logisti.locale`, written by Unity's player-pref selector when the selected locale changes.
3. The device language.
4. English, when the device language is not one of the three locales.

Turkish and German string tables fall back to English for a missing key. Choosing an unsupported code at runtime also selects English.

`LocaleService.TrySelect` does nothing while initialization is still running. The verification view stays blank until strings arrive.

Keys used now are only the foundation check (`ui.verification.*` and `ui.language.*`). Later copy should keep the same stable key shape: `ui`, `map`, `fleet`, `contracts`, `warehouse`, `market`, `company`, `tutorial`, `errors`, `notifications`, `events`, `products`, `cities`, `vehicles`.

Smart strings carry the greeting (`{player}`) and the delivery plural (`{count}`).

Formatting cultures are separate from the currency: `en` uses `en-US`, `tr` uses `tr-TR`, and `de` uses `de-DE`. Dates use the `DateTimeOffset` the caller supplies and are not shifted to the device time zone.

## Screen layout

Player orientation stays on auto-rotation. Portrait upside-down is disabled. The world map should call `ScreenLayouts.Apply(Landscape)`. Other main screens should call `Portrait`. `SafeAreaRect` insets one full-screen rectangle to `Screen.safeArea`. Unity has no safe-area callback, so the component compares the screen and safe rect while it is enabled and does nothing when they are unchanged.

## Performance

Target: 60 fps, about 16.67 ms, on suitable devices. That number is not a measured result.

The URP asset `Assets/Settings/UniversalRP.asset` still uses the template Renderer2D, with the SRP Batcher enabled and MSAA disabled. It was not modified. Its shadow settings are the template defaults and should be judged from a real capture before they are changed. Adaptive Performance is flagged on the asset, and the Adaptive Performance package is not installed.

`MobileFrameRate` sets `vSyncCount` to 0 and `targetFrameRate` to 60 once. Mobile devices ignore vSync. Release builds do not keep an on-screen profiler.

UI text refreshes when the locale changes. Logs are not written per frame. `GameLog.Info` is compiled out of release players. Errors stay, and logs must not include credentials or tokens.

## Profiling checklist

Do not record editor FPS as a device baseline.

1. In the editor, open the verification scene, enter Play mode, and attach the Unity Profiler. Use this to see managed allocations and whether the foundation scripts show up in `Update`. It is not a GPU baseline.
2. For a device capture, make a Development Build with Autoconnect Profiler and Script Debugging off unless you need a breakpoint. Connect the Profiler by USB.
3. Enable Frame Timing Stats only for that session (Player Settings, or `PlayerSettings.enableFrameTimingStats`). Turn it off afterward.
4. On Android, confirm the Android Build Support module is installed, then capture CPU, rendering, and memory modules while the verification scene is idle and while switching language. Record median frame time, GC allocations in `PlayerLoop`, batches, and set-pass calls.
5. On iOS, archive or build a development player and capture with the Unity Profiler or Instruments (Time Profiler and Game Memory). Use a physical device.
6. Compare against 16.67 ms. Keep a capture only after the scene has finished loading. Do not treat the first frames as the baseline.

## Platforms

Android scripting backend is already IL2CPP, ARM64. iOS is IL2CPP. Managed stripping was left at the project default. Do not raise it until a player build has been run; the localization package ships its own link.xml.

The Android application id is still the template value `com.DefaultCompany.urp_2d`. No store id, keystore, or provisioning profile was created.

Android Build Support is not installed with this editor. iOS Build Support is installed. Neither platform was built.

## Setup

`LogistiCore/Foundation/Setup` creates the locales, the Foundation string table, and the verification scene if that scene is missing. It is safe to run again. `LogistiCore/Foundation/Recreate Verification Scene` replaces the scene. The editor also retries setup on load until those assets exist.

The verification scene is `Assets/Game/Scenes/FoundationVerification.unity`. It is added after the template sample scene, not instead of it.

`LogistiCore/Foundation/Run Edit Mode Tests` runs the edit-mode suite in the open editor.

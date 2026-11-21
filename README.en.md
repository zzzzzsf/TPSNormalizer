# TPS Normalizer 0.3.2

By Zenice · MIT · [中文](README.zh-CN.md) · [Source](https://github.com/zzzzzsf/TPSNormalizer)

Experimental client plugin for **SPT 4.0.13 / EFT 0.16.9.40087**. It aims to reduce render-frame-rate differences in selected local-player camera, weapon-animation and stance paths.

**Offline checks pass. The maintainer reports successful in-game testing of the main functions in SPT 4.0.13.** See [the maintainer test report](MAINTAINER-TEST-REPORT.md) for scope and evidence limits. Clean-install verification, detailed measurements and Fika/SAIN runtime compatibility are not confirmed. This is not an FPS booster, a frame limiter, or a guarantee that every frame-dependent behavior is corrected.

## Features

- Ten individually configurable corrections: rotation limit, aim sway impulse, camera recoil blending, motion tracking, visual timing, ADS/look smoothing, FOV transitions, alternate directional mouse axes, lean inertia and rotation sound averaging.
- Global reference FPS: 30–360, default 120. Each correction can use its own reference FPS.
- Chinese / English settings labels, editable numeric inputs, apply/reset buttons.
- Optional passive diagnostic logging, off by default. Diagnostics stay in the local BepInEx log.

The reference value changes the response baseline, not the actual rendering frame rate. Larger values are not necessarily better. Normal mouse displacement, weapon stats, ballistics, global time scale and the global physics timestep are not changed.

## Requirements and compatibility

- Target build: SPT 4.0.13 / EFT 0.16.9.40087 only. Other SPT/EFT versions are unverified.
- BepInEx supplied with the target SPT installation (built against 5.4.23.2) and its Harmony library.
- The F12 interface uses ConfigurationManager supplied with SPT. It is not redistributed here.
- No server mod or additional downloaded runtime dependency. .NET SDK is needed only to build source.
- Fika 2.2.3 and SAIN 4.4.0 have had limited static inspection only; runtime compatibility is **unverified**.

## Installation

1. Close the game. Remove any older copy of `StableGame.dll` to prevent duplicate loading.
2. Extract the release ZIP and merge its `BepInEx` folder into your SPT root. The resulting runtime path must be:

   ```text
   <SPT root>/BepInEx/plugins/StableGame/StableGame.dll
   ```

3. Start SPT, open F12 and find `Zenice-TPSNormalizer` / TPS Normalizer. Choose Eng in the plugin's Language setting if needed.
4. Start with the master switch and all ten corrections enabled, global reference FPS 120, per-fix overrides disabled.
5. Follow TESTING.md for manual checks. It currently contains the detailed Chinese test guide; the essential comparison is the same action at actual 30/60/120 FPS with this plugin enabled and disabled.

The persistent GUID is `local.stablegame.fpsnormalizer`; configuration remains at `BepInEx/config/local.stablegame.fpsnormalizer.cfg`. Settings apply immediately; finish the current action and start a new one before comparing. Press Enter, Apply, or leave the numeric field to commit a value; Esc cancels.

## Uninstallation

Close the game and delete `BepInEx/plugins/StableGame/`. Optionally delete its configuration file. Restart the game to compare with the plugin completely unloaded. The master switch temporarily disables corrections.

The runtime ZIP contains only the plugin DLL. Documentation, licenses and reports are provided separately in the repository/source archive.

## Limitations and testing

417 IL/math assertions, 13,417 managed behavior assertions and 118 real-assembly Mono integration assertions are checked locally. The managed behavior model does not run native Unity, a real raid, the actual F12 window, or native SmoothDamp. Managed performance figures are not live game frame times. See reports and PERFORMANCE.md for details.

Dynamic targets, lost input samples, random motion, collisions, IK, animation events and other mods can still cause differences. Some corrected quaternion interpolation uses Slerp and can differ from the original Lerp trajectory. Rotation-limit corrections may have no visible effect unless the limit is reached; aim-sway correction may have no visible effect when the relevant game strength is zero. Disable a correction if it conflicts with another mod. Patch shape failures are logged and this mod's patches are removed.

No update checks, remote APIs, analytics, external downloads, installers, drivers or outbound network requests are implemented by this plugin. It does not write profiles or modify core game files; BepInEx handles its local config and logs.

## Source, support and license

- [Build instructions](BUILDING.md); complete runtime source in `src/`.
- [Source and issue tracker](https://github.com/zzzzzsf/TPSNormalizer). Include SPT/EFT versions, mod version, conflicting mods and reproduction steps. Review logs for private information before sharing them.
- [MIT license](LICENSE) and [dependency/provenance notices](THIRD-PARTY-NOTICES.md).
- This is one mod by Zenice, developed with GPT-generated code as disclosed by the maintainer. No third-party textures or game binaries are bundled.
- All downloads, features and updates are free. No donor-only content or early access.

0.3.2 updates version metadata and startup log text only; gameplay correction behavior is unchanged from 0.3.1. The manual confirmation refers to 0.3.1; no separate 0.3.2 gameplay test has been reported. Manual test confirmation is maintainer-reported; the offline test reports do not independently execute gameplay.


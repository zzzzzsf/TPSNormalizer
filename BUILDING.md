# Building TPS Normalizer 0.3.1

## Requirements

- Windows with PowerShell 7 (`pwsh`) and .NET 10 SDK (`dotnet`).
- Your own working SPT 4.0.13 / EFT 0.16.9.40087 installation, with BepInEx 5.4.23.2 and its supplied Harmony library.
- No NuGet packages, private source components, server mod, or decompiled game source are needed to build the plugin.

## Compile the public source

1. Download or clone this source repository and open PowerShell in its root.
2. Set the game directory argument to your own SPT root (the directory containing `EscapeFromTarkov_Data` and `BepInEx`).
3. Run:

   ```powershell
   dotnet --list-sdks
   pwsh -NoProfile -File ./build.ps1 -GamePath 'D:\Games\SPT'
   ```

4. The compiled plugin is `dist/StableGame/BepInEx/plugins/StableGame/StableGame.dll`.
5. With the game closed, copy that DLL to `BepInEx/plugins/StableGame/` in your chosen SPT installation.

`build.ps1` compiles every `src/*.cs` file using Roslyn with optimization and deterministic compilation enabled. It reads references from your game installation and writes only into this source tree. No third-party binaries need to be added to the repository. The source archive's `SOURCE-SHA256SUMS.txt` identifies the exported files.

## Verification limits

The development workspace also runs IL, managed behavior, Mono integration and managed performance checks. Those tools use local game dependencies and, for the behavior model, a proprietary decompiled BetterSpring fixture. That fixture and the full internal test workspace are deliberately excluded from the public source. They are not required to compile the distributed runtime DLL. Public offline reports document results from that workspace; they do not imply that the full local test suite is self-contained in this repository.

Follow the manual checks in TESTING.md to test the real client. The maintainer reports that 0.3.1's main functions work in SPT 4.0.13; see MAINTAINER-TEST-REPORT.md. No clean-install gameplay result or detailed measurements have been supplied. Identical DLL hashes are only expected with matching compiler and reference assemblies; the release reports record the local reference hashes.

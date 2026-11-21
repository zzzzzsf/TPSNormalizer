param([string]$GamePath = 'D:\Game\EFT')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$managedPath = Join-Path $GamePath 'EscapeFromTarkov_Data\Managed'
$corePath = Join-Path $GamePath 'BepInEx\core'
$outputPath = Join-Path $projectRoot 'dist\StableGame\BepInEx\plugins\StableGame'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$sdkLine = (dotnet --list-sdks | Select-Object -Last 1)
if (-not $sdkLine) { throw '.NET SDK is required to build.' }
$sdkVersion = $sdkLine.Split(' ')[0]
$sdkBase = ($sdkLine -split '\[')[1].TrimEnd(']')
$compiler = Join-Path $sdkBase "$sdkVersion\Roslyn\bincore\csc.dll"
$references = @('mscorlib.dll','System.dll','System.Core.dll','netstandard.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','Assembly-CSharp.dll','Comfort.dll','Comfort.Unity.dll') |
    ForEach-Object { '/reference:' + (Join-Path $managedPath $_) }
$references += @('BepInEx.dll','0Harmony.dll') | ForEach-Object { '/reference:' + (Join-Path $corePath $_) }
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& dotnet $compiler /nologo /noconfig /nostdlib+ /target:library /optimize+ /deterministic+ /langversion:latest "/out:$outputPath\StableGame.dll" @references @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host "Built $outputPath\StableGame.dll (game directory used read-only)."

$ErrorActionPreference = "Stop"

# 1. Resolve game path
$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
if (-not (Test-Path "$GamePath\RimWorldWin64.exe")) {
    Write-Error "RimWorld game directory not found at '$GamePath'."
    return
}

# 2. Setup folders
$modDir = "c:\Users\meast\Downloads\Development\GAMES\rimworldModding\AndroidTiersContinuedPatch"
$assembliesDir = "$modDir\1.6\Assemblies"
if (-not (Test-Path $assembliesDir)) {
    New-Item -ItemType Directory -Path $assembliesDir -Force | Out-Null
}

# 3. Locate Harmony DLL
$harmonyTarget = "$assembliesDir\0Harmony.dll"
if (-not (Test-Path $harmonyTarget)) {
    $gcHarmony = "c:\Users\meast\Downloads\Development\GAMES\rimworldModding\TheGarbageCollector\1.6\Assemblies\0Harmony.dll"
    if (Test-Path $gcHarmony) {
        Copy-Item $gcHarmony $harmonyTarget -Force
        Write-Host "Copied Harmony DLL from TheGarbageCollector." -ForegroundColor Green
    }
}

# 4. Compile AndroidTiersContinuedPatch.dll
Write-Host "Compiling AndroidTiersContinuedPatch..." -ForegroundColor Green
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    Write-Error "C# compiler not found at '$cscPath'."
    return
}

$outputDll = "$assembliesDir\AndroidTiersContinuedPatch.dll"
$managedDir = "$GamePath\RimWorldWin64_Data\Managed"

# Path to AndroidTiersContinued.dll
$atcDll = "C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3711019495\1.6\Assemblies\AndroidTiersContinued.dll"

# Compiler references
$refs = @(
    "$managedDir\Assembly-CSharp.dll",
    "$managedDir\UnityEngine.dll",
    "$managedDir\UnityEngine.CoreModule.dll",
    "$managedDir\netstandard.dll",
    "$managedDir\Unity.Mathematics.dll",
    "$assembliesDir\0Harmony.dll",
    "$atcDll",
    "System.dll",
    "System.Core.dll",
    "System.Xml.dll"
)

$refArgs = $refs | ForEach-Object { "/r:`"$_`"" }

# Get all C# files under Source directory recursively
$sources = Get-ChildItem -Path "$modDir\Source" -Filter "*.cs" -Recurse | ForEach-Object { "`"$($_.FullName)`"" }

$cmdArgs = @("/target:library", "/out:`"$outputDll`"", "/nowarn:1684") + $refArgs + $sources

& $cscPath $cmdArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Compilation failed." -ForegroundColor Red
    return
}
Write-Host "Successfully compiled AndroidTiersContinuedPatch.dll!" -ForegroundColor Green

# 5. Deploy mod to RimWorld Mods folder
$deployDir = "$GamePath\Mods\AndroidTiersContinuedPatch"
Write-Host "Deploying mod to: $deployDir" -ForegroundColor Green

# Clean previous deployment
if (Test-Path $deployDir) {
    Remove-Item $deployDir -Recurse -Force
}

# Create deployment structure
New-Item -ItemType Directory -Path "$deployDir\1.6\Assemblies" -Force | Out-Null

# Copy asset folders recursively if they exist
foreach ($dir in @("About", "Defs", "Patches", "Textures", "Languages")) {
    if (Test-Path "$modDir\$dir") {
        New-Item -ItemType Directory -Path "$deployDir\$dir" -Force | Out-Null
        Copy-Item -Path "$modDir\$dir\*" -Destination "$deployDir\$dir" -Recurse -Force
    }
}

# Copy assemblies
Copy-Item "$assembliesDir\AndroidTiersContinuedPatch.dll" "$deployDir\1.6\Assemblies\" -Force
if (Test-Path $harmonyTarget) {
    Copy-Item $harmonyTarget "$deployDir\1.6\Assemblies\" -Force
}

Write-Host "Android Tiers Continued Patch deployed successfully!" -ForegroundColor Green

# 1. Resolve game path
$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
if (-not (Test-Path "$GamePath\RimWorldWin64.exe")) {
    Write-Error "RimWorld game directory not found at '$GamePath'."
    return
}

# 2. Setup folders
$modName = "InfiniteTurrets"
$modDir = $PSScriptRoot
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
        Write-Host "Copied Harmony DLL." -ForegroundColor Green
    }
}

# 4. Compile $modName.dll
Write-Host "Compiling $modName..." -ForegroundColor Green
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    Write-Error "C# compiler not found at '$cscPath'."
    return
}

$outputDll = "$assembliesDir\$modName.dll"
$managedDir = "$GamePath\RimWorldWin64_Data\Managed"

# Compiler references
$refs = @(
    "$managedDir\Assembly-CSharp.dll",
    "$managedDir\UnityEngine.dll",
    "$managedDir\UnityEngine.CoreModule.dll",
    "$managedDir\UnityEngine.TextRenderingModule.dll",
    "$managedDir\UnityEngine.IMGUIModule.dll",
    "$managedDir\netstandard.dll",
    "$managedDir\Unity.Mathematics.dll",
    "$assembliesDir\0Harmony.dll",
    "System.dll",
    "System.Core.dll",
    "System.Xml.dll"
)

$refArgs = $refs | ForEach-Object { "/r:`"$_`"" }

# Get all C# files under Source directory
$sources = Get-ChildItem -Path "$modDir\Source" -Filter "*.cs" -Recurse | ForEach-Object { "`"$($_.FullName)`"" }

$cmdArgs = @("/target:library", "/out:`"$outputDll`"", "/unsafe") + $refArgs + $sources

& $cscPath $cmdArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Compilation failed." -ForegroundColor Red
    return
}
Write-Host "Successfully compiled $modName.dll!" -ForegroundColor Green

# 5. Deploy mod to RimWorld Mods folder
$deployDir = "$GamePath\Mods\$modName"
Write-Host "Deploying mod to: $deployDir" -ForegroundColor Green

# Clean previous deployment
if (Test-Path $deployDir) {
    Remove-Item $deployDir -Recurse -Force
}

# Create deployment structure
New-Item -ItemType Directory -Path "$deployDir\1.6\Assemblies" -Force | Out-Null
New-Item -ItemType Directory -Path "$deployDir\1.5\Assemblies" -Force | Out-Null
New-Item -ItemType Directory -Path "$deployDir\1.4\Assemblies" -Force | Out-Null

# Copy asset folders recursively if they exist
foreach ($dir in @("About", "Defs", "Patches", "Textures", "Languages")) {
    if (Test-Path "$modDir\$dir") {
        New-Item -ItemType Directory -Path "$deployDir\$dir" -Force | Out-Null
        Copy-Item -Path "$modDir\$dir\*" -Destination "$deployDir\$dir" -Recurse -Force
    }
}

# Copy assemblies to all supported versions
foreach ($ver in @("1.6", "1.5", "1.4")) {
    $targetAssembliesDir = "$deployDir\$ver\Assemblies"
    Copy-Item "$assembliesDir\$modName.dll" "$targetAssembliesDir\" -Force
    if (Test-Path "$assembliesDir\0Harmony.dll") {
        Copy-Item "$assembliesDir\0Harmony.dll" "$targetAssembliesDir\" -Force
    }
}

Write-Host "$modName mod deployed successfully!" -ForegroundColor Green

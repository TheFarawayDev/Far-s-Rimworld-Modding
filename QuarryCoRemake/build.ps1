# 1. Resolve game path
$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
if (-not (Test-Path "$GamePath\RimWorldWin64.exe")) {
    Write-Error "RimWorld game directory not found at '$GamePath'."
    return
}

# 2. Setup folders
$modDir = "c:\Users\meast\Downloads\Development\GAMES\rimworldModding\QuarryCoRemake"
$assembliesDir = "$modDir\Assemblies"
if (-not (Test-Path $assembliesDir)) {
    New-Item -ItemType Directory -Path $assembliesDir -Force | Out-Null
}

# 3. Compile QuarryCo.dll
Write-Host "Compiling QuarryCoRemake..." -ForegroundColor Green
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    Write-Error "C# compiler not found at '$cscPath'."
    return
}

$outputDll = "$assembliesDir\QuarryCo.dll"
$managedDir = "$GamePath\RimWorldWin64_Data\Managed"

# Compiler references
$refs = @(
    "$managedDir\Assembly-CSharp.dll",
    "$managedDir\UnityEngine.dll",
    "$managedDir\UnityEngine.CoreModule.dll",
    "$managedDir\UnityEngine.IMGUIModule.dll",
    "$managedDir\UnityEngine.TextRenderingModule.dll",
    "$managedDir\netstandard.dll",
    "$managedDir\Unity.Mathematics.dll",
    "System.dll",
    "System.Core.dll",
    "System.Xml.dll"
)

$refArgs = $refs | ForEach-Object { "/r:`"$_`"" }

# Get all C# files under Source directory
$sources = Get-ChildItem -Path "$modDir\Source" -Filter "*.cs" | ForEach-Object { "`"$($_.FullName)`"" }

$cmdArgs = @("/target:library", "/out:`"$outputDll`"", "/unsafe", "/nowarn:1684") + $refArgs + $sources

& $cscPath $cmdArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Compilation failed." -ForegroundColor Red
    return
}
Write-Host "Successfully compiled QuarryCo.dll!" -ForegroundColor Green

# 4. Deploy mod to RimWorld Mods folder
$deployDir = "$GamePath\Mods\QuarryCoRemake"
Write-Host "Deploying mod to: $deployDir" -ForegroundColor Green

# Clean previous deployment
if (Test-Path $deployDir) {
    Remove-Item $deployDir -Recurse -Force
}

# Create deployment structure
New-Item -ItemType Directory -Path "$deployDir\Assemblies" -Force | Out-Null

# Copy asset folders recursively if they exist
foreach ($dir in @("About", "Defs", "Patches", "Textures", "Languages")) {
    if (Test-Path "$modDir\$dir") {
        New-Item -ItemType Directory -Path "$deployDir\$dir" -Force | Out-Null
        Copy-Item -Path "$modDir\$dir\*" -Destination "$deployDir\$dir" -Recurse -Force
    }
}

# Copy assemblies
Copy-Item "$assembliesDir\QuarryCo.dll" "$deployDir\Assemblies\" -Force

Write-Host "QuarryCoRemake mod deployed successfully!" -ForegroundColor Green

# 1. Resolve game path
$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
if (-not (Test-Path "$GamePath\RimWorldWin64.exe")) {
    Write-Error "RimWorld game directory not found at '$GamePath'."
    return
}

# 2. Setup folders
$modDir = "c:\Users\meast\Downloads\Development\GAMES\rimworldModding\TheGarbageCollector"
$assembliesDir = "$modDir\1.6\Assemblies"
if (-not (Test-Path $assembliesDir)) {
    New-Item -ItemType Directory -Path $assembliesDir -Force | Out-Null
}

# 3. Locate Harmony DLL
$harmonyTarget = "$assembliesDir\0Harmony.dll"
if (-not (Test-Path $harmonyTarget)) {
    Write-Host "Locating Harmony DLL in Steam Workshop files..." -ForegroundColor Gray
    
    # Try the 1.6 known path first
    $knownPath = "C:\Program Files (x86)\Steam\steamapps\workshop\temp\294100\872762753\1.6\Assemblies\0Harmony.dll"
    if (-not (Test-Path $knownPath)) {
        # Fallback to 1.5 if 1.6 is not in temp yet
        $knownPath = "C:\Program Files (x86)\Steam\steamapps\workshop\temp\294100\872762753\1.5\Assemblies\0Harmony.dll"
    }
    
    if (Test-Path $knownPath) {
        Copy-Item $knownPath $harmonyTarget -Force
        Write-Host "Found Harmony DLL at: $knownPath" -ForegroundColor Green
    } else {
        # Search recursively in Workshop folder for 1.6 or 1.5 compatible Harmony DLL
        $found = Get-ChildItem -Path "C:\Program Files (x86)\Steam\steamapps\workshop" -Filter "0Harmony.dll" -Recurse -ErrorAction SilentlyContinue | 
                 Where-Object { $_.FullName -like "*1.6*" -or $_.FullName -like "*1.5*" } | 
                 Select-Object -First 1
                 
        if ($found) {
            Copy-Item $found.FullName $harmonyTarget -Force
            Write-Host "Found Harmony DLL at: $($found.FullName)" -ForegroundColor Green
        } else {
            Write-Host "Could not find a local 0Harmony.dll. Please ensure you have subscribed to the Harmony mod on Steam Workshop." -ForegroundColor Red
            return
        }
    }
} else {
    Write-Host "Harmony DLL already present in Assemblies folder." -ForegroundColor Gray
}
# 3.5. Copy FarUtils DLL from FarUtils mod folder
$farUtilsSource = "$modDir\..\FarUtils\1.6\Assemblies\FarUtils.dll"
if (Test-Path $farUtilsSource) {
    Copy-Item $farUtilsSource "$assembliesDir\FarUtils.dll" -Force
    Write-Host "Copied FarUtils DLL from FarUtils framework mod." -ForegroundColor Green
} else {
    Write-Error "FarUtils.dll not found in FarUtils framework mod folder. Please compile FarUtils first."
    return
}

# 4. Compile TheGarbageCollector.dll
Write-Host "Compiling TheGarbageCollector..." -ForegroundColor Green
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    Write-Error "C# compiler not found at '$cscPath'."
    return
}

$outputDll = "$assembliesDir\TheGarbageCollector.dll"
$managedDir = "$GamePath\RimWorldWin64_Data\Managed"

# Compiler references
$refs = @(
    "$managedDir\Assembly-CSharp.dll",
    "$managedDir\UnityEngine.dll",
    "$managedDir\UnityEngine.CoreModule.dll",
    "$managedDir\netstandard.dll",
    "$managedDir\Unity.Mathematics.dll",
    "$assembliesDir\0Harmony.dll",
    "$assembliesDir\FarUtils.dll",
    "System.dll",
    "System.Core.dll",
    "System.Xml.dll"
)

$refArgs = $refs | ForEach-Object { "/r:`"$_`"" }

# Get all C# files under Source directory
$sources = Get-ChildItem -Path "$modDir\Source" -Filter "*.cs" | ForEach-Object { "`"$($_.FullName)`"" }

$cmdArgs = @("/target:library", "/out:`"$outputDll`"", "/unsafe") + $refArgs + $sources

& $cscPath $cmdArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Compilation failed." -ForegroundColor Red
    return
}
Write-Host "Successfully compiled TheGarbageCollector.dll!" -ForegroundColor Green

# 5. Deploy mod to RimWorld Mods folder
$deployDir = "$GamePath\Mods\TheGarbageCollector"
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
Copy-Item "$assembliesDir\TheGarbageCollector.dll" "$deployDir\1.6\Assemblies\" -Force
Copy-Item "$assembliesDir\0Harmony.dll" "$deployDir\1.6\Assemblies\" -Force
Copy-Item "$assembliesDir\FarUtils.dll" "$deployDir\1.6\Assemblies\" -Force

Write-Host "The Garbage Collector mod deployed successfully! You can now enable it in RimWorld's mod menu." -ForegroundColor Green

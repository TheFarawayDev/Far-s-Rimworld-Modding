# 1. Resolve game path
$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
if (-not (Test-Path "$GamePath\RimWorldWin64.exe")) {
    Write-Error "RimWorld game directory not found at '$GamePath'."
    return
}

# 2. Setup folders
$modDir = "c:\Users\meast\Downloads\Development\GAMES\rimworldModding\CE_Embrasures"

# 3. Deploy mod to RimWorld Mods folder
$deployDir = "$GamePath\Mods\CE_Embrasures"
Write-Host "Deploying mod to: $deployDir" -ForegroundColor Green

# Clean previous deployment
if (Test-Path $deployDir) {
    Remove-Item $deployDir -Recurse -Force
}

# Create deployment structure
New-Item -ItemType Directory -Path "$deployDir" -Force | Out-Null

# Copy asset folders recursively if they exist
foreach ($dir in @("About", "Defs", "Patches", "Textures", "Languages")) {
    if (Test-Path "$modDir\$dir") {
        New-Item -ItemType Directory -Path "$deployDir\$dir" -Force | Out-Null
        Copy-Item -Path "$modDir\$dir\*" -Destination "$deployDir\$dir" -Recurse -Force
    }
}

Write-Host "CE_Embrasures mod deployed successfully!" -ForegroundColor Green

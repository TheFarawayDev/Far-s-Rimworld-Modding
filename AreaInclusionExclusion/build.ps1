$ErrorActionPreference = "Stop"

$modName = "AreaInclusionExclusion"
$csprojPath = Join-Path $PSScriptRoot "$modName.csproj"
$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld"

Write-Host "Compiling $modName using latest C# version..." -ForegroundColor Cyan

dotnet build $csprojPath -c Release --no-incremental -p:LangVersion=preview

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to compile mod: $modName"
    exit 1
}

Write-Host "Successfully compiled $modName!" -ForegroundColor Green

if (-not (Test-Path "$GamePath\RimWorldWin64.exe")) {
    Write-Warning "RimWorld game directory not found at '$GamePath'. Skipping deployment."
    exit 0
}

$deployDir = "$GamePath\Mods\$modName"
Write-Host "Deploying mod to: $deployDir" -ForegroundColor Green

if (Test-Path $deployDir) {
    Remove-Item $deployDir -Recurse -Force
}
New-Item -ItemType Directory -Path $deployDir -Force | Out-Null

foreach ($dir in @("About", "Defs", "Patches", "Textures", "Languages", "Sounds", "LoadFolders.xml")) {
    $sourcePath = Join-Path $PSScriptRoot $dir
    if (Test-Path $sourcePath) {
        Copy-Item -Path $sourcePath -Destination "$deployDir\" -Recurse -Force
    }
}

# Copy assemblies based on supported versions in About.xml
$aboutXmlPath = Join-Path $PSScriptRoot "About\About.xml"
if (Test-Path $aboutXmlPath) {
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load([System.IO.Path]::GetFullPath($aboutXmlPath))
    if ($null -ne $xml.ModMetaData.supportedVersions) {
        foreach ($ver in $xml.ModMetaData.supportedVersions.li) {
            $deployVersionDir = "$deployDir\$ver\Assemblies"
            if (-not (Test-Path $deployVersionDir)) {
                New-Item -ItemType Directory -Path $deployVersionDir -Force | Out-Null
            }
            $sourceAssemblies = Join-Path $PSScriptRoot "1.6\Assemblies\*"
            if (Test-Path (Join-Path $PSScriptRoot "1.6\Assemblies")) {
                Copy-Item -Path $sourceAssemblies -Destination $deployVersionDir -Recurse -Force
            }
        }
    }
}

Write-Host "$modName deployed successfully!" -ForegroundColor Green

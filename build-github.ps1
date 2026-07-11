# build-github.ps1
# Script to compile RimWorld mods on GitHub Actions or locally without a RimWorld installation.

$ErrorActionPreference = "Stop"

# Define the mods, their compilation order, and their dependencies
$mods = @(
    @{
        Name         = "AreaInclusionExclusion"
        NeedsHarmony = $true
    },
    @{
        Name         = "CallForATrader"
        NeedsHarmony = $true
    },
    @{
        Name         = "TheGarbageCollector"
        NeedsHarmony = $true
    },
    @{
        Name              = "AndroidTiersContinuedPatch"
        NeedsHarmony      = $true
        NeedsAndroidTiers = $true
    },
    @{
        Name         = "CE_Embrasures"
        NeedsHarmony = $false
        IsXmlOnly    = $true
    },
    @{
        Name         = "InfiniteTurrets"
        NeedsHarmony = $true
    },
    @{
        Name         = "ModifiedWorkNeeded"
        NeedsHarmony = $false
    },
    @{
        Name         = "DoMoreResearch"
        NeedsHarmony = $true
    }
)

$rimworldRefVersion = "1.6.4850"
$harmonyVersion = "2.3.3"
$rootPath = Resolve-Path .

Write-Host "Starting RimWorld Mod Compilation..." -ForegroundColor Cyan

foreach ($mod in $mods) {
    $modName = $mod.Name
    $modDir = Join-Path $rootPath $modName
    $csprojPath = Join-Path $modDir "$modName.csproj"
    
    Write-Host "----------------------------------------" -ForegroundColor Gray
    Write-Host "Preparing build for mod: $modName" -ForegroundColor Yellow

    if ($mod.IsXmlOnly) {
        Write-Host "Mod $modName is XML/Assets only. Skipping C# compilation." -ForegroundColor Green
        continue
    }

    # Define .csproj content
    $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <OutputType>Library</OutputType>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <OutputPath>1.6/Assemblies</OutputPath>
    <Optimize>true</Optimize>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <LangVersion>latest</LangVersion>
    <Deterministic>true</Deterministic>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Krafs.Rimworld.Ref" Version="$rimworldRefVersion" />
  </ItemGroup>
"@

    if ($mod.NeedsHarmony) {
        $csprojContent += @"

  <ItemGroup>
    <PackageReference Include="Lib.Harmony" Version="$harmonyVersion" />
  </ItemGroup>
"@
    }


    if ($mod.NeedsAndroidTiers) {
        $csprojContent += @"

  <ItemGroup>
    <Reference Include="AndroidTiersContinued">
      <HintPath>lib/AndroidTiersContinued.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
"@
    }

    $csprojContent += "`n</Project>"

    # Write the temporary .csproj file
    Write-Host "Generating temporary project file: $csprojPath" -ForegroundColor Gray
    Set-Content -Path $csprojPath -Value $csprojContent

    try {
        Write-Host "Compiling $modName via dotnet build..." -ForegroundColor Green
        # Run dotnet build in the mod directory
        dotnet build $csprojPath -c Release --no-incremental
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to compile mod: $modName"
        }
        
        # Copy compiled assemblies to all supported versions from About.xml
        $aboutXmlPath = Join-Path $modDir "About\About.xml"
        if (Test-Path $aboutXmlPath) {
            $xml = New-Object System.Xml.XmlDocument
            $xml.Load([System.IO.Path]::GetFullPath($aboutXmlPath))
            if ($null -ne $xml.ModMetaData.supportedVersions) {
                $supportedVersions = $xml.ModMetaData.supportedVersions.li
                foreach ($ver in $supportedVersions) {
                    if ($ver -ne "1.6") {
                        $targetDir = Join-Path $modDir "$ver\Assemblies"
                        if (-not (Test-Path $targetDir)) {
                            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
                        }
                        Copy-Item -Path (Join-Path $modDir "1.6\Assemblies\*") -Destination $targetDir -Recurse -Force
                    }
                }
            }
        }
        
        Write-Host "Successfully compiled $modName!" -ForegroundColor Green
    }
    catch {
        Write-Error "Error compiling ${modName}: $_"
        exit 1
    }
    finally {
        # Clean up the temporary project files and build artifacts
        if (Test-Path $csprojPath) {
            Write-Host "Removing temporary project file: $csprojPath" -ForegroundColor Gray
            Remove-Item $csprojPath -Force
        }
        
        $objDir = Join-Path $modDir "obj"
        if (Test-Path $objDir) {
            Write-Host "Removing temporary obj folder: $objDir" -ForegroundColor Gray
            Remove-Item $objDir -Recurse -Force
        }
    }
}

Write-Host "----------------------------------------" -ForegroundColor Gray
Write-Host "All mods compiled successfully!" -ForegroundColor Green

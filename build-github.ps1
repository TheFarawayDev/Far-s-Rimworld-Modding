# build-github.ps1
# Script to compile RimWorld mods on GitHub Actions or locally without a RimWorld installation.

$ErrorActionPreference = "Stop"

# Define the mods, their compilation order, and their dependencies
$mods = @(
    @{
        Name = "FarUtils"
        NeedsHarmony = $true
        NeedsFarUtils = $false
    },
    @{
        Name = "BlightedAlert"
        NeedsHarmony = $false
        NeedsFarUtils = $true
    },
    @{
        Name = "AreaInclusionExclusion"
        NeedsHarmony = $true
        NeedsFarUtils = $true
    },
    @{
        Name = "CallForATrader"
        NeedsHarmony = $true
        NeedsFarUtils = $true
    },
    @{
        Name = "TheGarbageCollector"
        NeedsHarmony = $true
        NeedsFarUtils = $true
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

    # Define .csproj content
    $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
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

    if ($mod.NeedsFarUtils) {
        $csprojContent += @"

  <ItemGroup>
    <Reference Include="FarUtils">
      <HintPath>..\FarUtils\1.6\Assemblies\FarUtils.dll</HintPath>
      <Private>true</Private>
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

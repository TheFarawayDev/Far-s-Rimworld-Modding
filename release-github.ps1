# release-github.ps1
# Script to compile RimWorld mods and handle automated versioning, tagging, and releasing.

$ErrorActionPreference = "Stop"

# Define the mods, their compilation order, and their dependencies
$mods = @(
    @{
        Name          = "FarUtils"
        NeedsHarmony  = $true
        NeedsFarUtils = $false
    },
    @{
        Name          = "BlightedAlert"
        NeedsHarmony  = $false
        NeedsFarUtils = $true
    },
    @{
        Name          = "AreaInclusionExclusion"
        NeedsHarmony  = $true
        NeedsFarUtils = $true
    },
    @{
        Name          = "CallForATrader"
        NeedsHarmony  = $true
        NeedsFarUtils = $true
    },
    @{
        Name          = "TheGarbageCollector"
        NeedsHarmony  = $true
        NeedsFarUtils = $true
    },
    @{
        Name          = "AndroidTiersContinuedPatch"
        NeedsHarmony  = $true
        NeedsFarUtils = $false
    }
)

$rimworldRefVersion = "1.6.4850"
$harmonyVersion = "2.3.3"
$rootPath = Resolve-Path .
$isRelease = $null -ne $env:IS_RELEASE -and $env:IS_RELEASE -eq "true"

Write-Host "Starting RimWorld Mod Compilation Workflow..." -ForegroundColor Cyan
if ($isRelease) {
    Write-Host "Release mode is ENABLED. Mod updates will be auto-versioned, tagged, and published." -ForegroundColor Green
}
else {
    Write-Host "Release mode is DISABLED. Performing compilation only." -ForegroundColor Yellow
}

# Setup git identity if running in GitHub Actions environment
if ($env:GITHUB_ACTIONS -eq "true" -and $isRelease) {
    Write-Host "Configuring git identity for GitHub Actions bot..." -ForegroundColor Gray
    git config --global user.name "github-actions[bot]"
    git config --global user.email "41898282+github-actions[bot]@users.noreply.github.com"
}

# Function to read and update version in About.xml
function Update-AboutXmlVersion {
    param(
        [string]$xmlPath,
        [string]$version
    )
    if (Test-Path $xmlPath) {
        Write-Host "Updating About.xml version to: $version" -ForegroundColor Gray
        $xml = New-Object System.Xml.XmlDocument
        $fullPath = [System.IO.Path]::GetFullPath($xmlPath)
        $xml.Load($fullPath)
        
        $meta = $xml.ModMetaData
        if ($null -ne $meta) {
            $versionNode = $meta.SelectSingleNode("modVersion")
            if ($null -eq $versionNode) {
                $versionNode = $xml.CreateElement("modVersion")
                $meta.AppendChild($versionNode) | Out-Null
            }
            $versionNode.InnerText = $version
            $xml.Save($fullPath)
            Write-Host "Successfully updated modVersion in About.xml to $version." -ForegroundColor Green
        }
        else {
            Write-Warning "Could not find <ModMetaData> node in About.xml."
        }
    }
    else {
        Write-Warning "About.xml not found at: $xmlPath"
    }
}

# Function to calculate next global version
function Get-NextGlobalVersion {
    param([string]$lastTag)
    if ([string]::IsNullOrEmpty($lastTag)) { return "1" }
    $versionStr = $lastTag -replace "^Release-", ""
    if ([int]::TryParse($versionStr, [ref]$null)) {
        return ([int]$versionStr + 1).ToString()
    }
    return "1"
}

# Function to calculate next mod version based on About.xml and changes
function Get-NextModVersion {
    param([string]$modName, [string]$lastGlobalTag)
    $aboutPath = Join-Path $modName "About\About.xml"
    $currentVer = "1.0.0"
    if (Test-Path $aboutPath) {
        $xml = New-Object System.Xml.XmlDocument
        $xml.Load([System.IO.Path]::GetFullPath($aboutPath))
        if ($null -ne $xml.ModMetaData -and $null -ne $xml.ModMetaData.modVersion) {
            $ver = $xml.ModMetaData.modVersion.Trim()
            if ($ver -match '^\d+\.\d+\.\d+$') { $currentVer = $ver }
        }
    }
    if ([string]::IsNullOrEmpty($lastGlobalTag)) { return $currentVer }
    
    # Check if mod directory changed
    $diffCount = (git diff --name-only $lastGlobalTag HEAD -- $modName | Measure-Object).Count
    if ($diffCount -gt 0) {
        $parts = $currentVer -split '\.'
        $major = [int]$parts[0]; $minor = [int]$parts[1]; $patch = [int]$parts[2]
        $commits = git log "$lastGlobalTag..HEAD" --oneline -- $modName
        $bump = "patch"
        foreach ($commit in $commits) {
            if ($commit -match '\[major\]|breaking|!') { $bump = "major"; break }
            elseif ($commit -match '\[minor\]|feat') { $bump = "minor" }
        }
        if ($bump -eq "major") { $major++; $minor = 0; $patch = 0 }
        elseif ($bump -eq "minor") { $minor++; $patch = 0 }
        else { $patch++ }
        return "$major.$minor.$patch"
    }
    return $currentVer
}

# We always build first, then release
foreach ($mod in $mods) {
    $modName = $mod.Name
    $modDir = Join-Path $rootPath $modName
    $csprojPath = Join-Path $modDir "$modName.csproj"
    
    Write-Host "----------------------------------------" -ForegroundColor Gray
    Write-Host "Compiling mod: $modName" -ForegroundColor Yellow

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
    Set-Content -Path $csprojPath -Value $csprojContent

    try {
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
        
        Write-Host "Successfully compiled $modName DLL(s)!" -ForegroundColor Green
    }
    finally {
        if (Test-Path $csprojPath) { Remove-Item $csprojPath -Force }
        $objDir = Join-Path $modDir "obj"
        if (Test-Path $objDir) { Remove-Item $objDir -Recurse -Force }
    }
}

# Handle versioning and release tags
if ($isRelease) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Processing Release and Tagging Automations..." -ForegroundColor Cyan

    $stagingParent = Join-Path $rootPath "staging"
    if (Test-Path $stagingParent) {
        Remove-Item $stagingParent -Recurse -Force
    }

    # Find the latest global Git tag (e.g. Release-*)
    $tags = @(git tag -l "Release-*" | Sort-Object { [int]($_ -replace "^Release-", "") } -Descending | Where-Object { $_ -ne "" })
    $latestGlobalTag = $null
    if ($tags.Count -gt 0) {
        $latestGlobalTag = $tags[0]
    }

    # Check if ANYTHING changed in any mod
    $anyChanges = $false
    if ($null -ne $latestGlobalTag) {
        foreach ($mod in $mods) {
            $diffCount = (git diff --name-only $latestGlobalTag HEAD -- $($mod.Name) | Measure-Object).Count
            if ($diffCount -gt 0) {
                $anyChanges = $true
                break
            }
        }
    } else {
        $anyChanges = $true
    }

    if (-not $anyChanges) {
        Write-Host "No changes detected in any mod since $latestGlobalTag. Skipping release." -ForegroundColor Yellow
    } else {
        $nextGlobalVersion = Get-NextGlobalVersion -lastTag $latestGlobalTag
        $globalTag = "Release-$nextGlobalVersion"
        Write-Host "Target Global Release Version: $nextGlobalVersion (Tag: $globalTag)" -ForegroundColor Cyan
        
        $zipPaths = @()
        $notesContent = @("### Release #$nextGlobalVersion", "")
        
        foreach ($mod in $mods) {
            $modName = $mod.Name
            $modDir = Join-Path $rootPath $modName
            Write-Host "----------------------------------------" -ForegroundColor Gray
            Write-Host "Packaging mod: $modName" -ForegroundColor Yellow
            
            $modVersion = Get-NextModVersion -modName $modName -lastGlobalTag $latestGlobalTag
            
            # Setup staging
            $stagingDir = Join-Path $stagingParent $modName
            New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

            # Copy relevant directories (excluding Source, obj, bin, and config files)
            $subDirs = Get-ChildItem -Path $modDir -Directory | Where-Object {
                $_.Name -ne "Source" -and 
                $_.Name -ne "obj" -and 
                $_.Name -ne "bin" -and 
                $_.Name -notlike ".*"
            }
            foreach ($dir in $subDirs) {
                $dest = Join-Path $stagingDir $dir.Name
                Copy-Item -Path $dir.FullName -Destination $dest -Recurse -Force
            }

            # Copy root files (e.g. README.md, LICENSE) excluding build and project files
            $files = Get-ChildItem -Path $modDir -File | Where-Object {
                $_.Name -notlike "*.csproj" -and
                $_.Name -notlike "*.user" -and
                $_.Name -ne "build.ps1" -and
                $_.Name -notlike ".*"
            }
            foreach ($file in $files) {
                Copy-Item -Path $file.FullName -Destination $stagingDir -Force
            }

            # Also update the source repository's About.xml and commit it if it changed
            $sourceAboutXml = Join-Path $modDir "About\About.xml"
            $oldVer = ""
            if (Test-Path $sourceAboutXml) {
                $xml = New-Object System.Xml.XmlDocument
                $xml.Load([System.IO.Path]::GetFullPath($sourceAboutXml))
                if ($null -ne $xml.ModMetaData -and $null -ne $xml.ModMetaData.modVersion) {
                    $oldVer = $xml.ModMetaData.modVersion.Trim()
                }
            }

            Update-AboutXmlVersion -xmlPath $sourceAboutXml -version $modVersion
            if ($oldVer -ne $modVersion -and $env:GITHUB_ACTIONS -eq "true") {
                git add $sourceAboutXml
                git commit -m "Bump $modName version to $modVersion"
            }

            # Update version in the staging copy of About.xml
            $stagingAboutXml = Join-Path $stagingDir "About\About.xml"
            Update-AboutXmlVersion -xmlPath $stagingAboutXml -version $modVersion

            # Compress mod archive
            $zipName = "$modName-$modVersion.zip"
            $zipPath = Join-Path $rootPath $zipName
            if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
            
            Write-Host "Creating zip package: $zipName" -ForegroundColor Gray
            Compress-Archive -Path $stagingDir -DestinationPath $zipPath -Force
            $zipPaths += $zipPath
            
            # Gather commits for release changelog
            $notesContent += "#### $modName (v$modVersion)"
            if ($oldVer -ne $modVersion -and $null -ne $latestGlobalTag) {
                $changelog = git log "$latestGlobalTag..HEAD" --oneline -- $modName
                if ($null -ne $changelog -and $changelog.Length -gt 0) {
                    foreach ($c in $changelog) {
                        $cleanCommit = $c -replace '^[a-f0-9]+\s+', ''
                        $notesContent += "- $cleanCommit"
                    }
                } else {
                    $notesContent += "- Internal updates."
                }
            } elseif ($oldVer -eq $modVersion) {
                $notesContent += "- No changes in this release."
            } else {
                $notesContent += "- Initial release or no previous tag."
            }
            $notesContent += ""
        }
        
        if ($env:GITHUB_ACTIONS -eq "true") {
            # Push all About.xml commits
            git push origin HEAD
        }

        $notesFile = [System.IO.Path]::GetTempFileName()
        $notesContent | Out-File -FilePath $notesFile -Encoding utf8

        # Tag repository and push to GitHub
        Write-Host "Tagging repository globally: $globalTag" -ForegroundColor Gray
        git tag $globalTag
        git push origin $globalTag

        # Create ONE GitHub release and upload ALL zips
        Write-Host "Creating GitHub Release and uploading assets..." -ForegroundColor Gray
        $ghArgs = @("release", "create", $globalTag, "--title", "Release #$nextGlobalVersion", "--notes-file", $notesFile)
        foreach ($zip in $zipPaths) { $ghArgs += $zip }
        
        & gh $ghArgs
        
        # Clean up temp files
        Remove-Item $notesFile -Force
        foreach ($zip in $zipPaths) { Remove-Item $zip -Force }
        Write-Host "Successfully completed global release $globalTag!" -ForegroundColor Green
    }

    # Clean up staging parent folder
    if (Test-Path $stagingParent) {
        Remove-Item $stagingParent -Recurse -Force
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Workflow completed successfully." -ForegroundColor Green

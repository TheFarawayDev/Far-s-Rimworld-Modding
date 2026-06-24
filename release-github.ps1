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
            Write-Host "Successfully updated modVersion in About.xml." -ForegroundColor Green
        }
        else {
            Write-Warning "Could not find <ModMetaData> node in About.xml."
        }
    }
    else {
        Write-Warning "About.xml not found at: $xmlPath"
    }
}

# Function to calculate next version based on Git tags and commits
function Get-NextVersion {
    param(
        [string]$modName,
        [string]$lastTag,
        [string]$defaultVersion
    )

    if ([string]::IsNullOrEmpty($lastTag)) {
        # Try reading existing version from About.xml
        $aboutPath = Join-Path $modName "About\About.xml"
        if (Test-Path $aboutPath) {
            $xml = New-Object System.Xml.XmlDocument
            $xml.Load([System.IO.Path]::GetFullPath($aboutPath))
            if ($null -ne $xml.ModMetaData -and $null -ne $xml.ModMetaData.modVersion) {
                $currentVer = $xml.ModMetaData.modVersion.Trim()
                if ($currentVer -match '^\d+\.\d+\.\d+$') {
                    return $currentVer
                }
            }
        }
        return $defaultVersion
    }

    # Extract version suffix from tag, e.g. "FarUtils-v1.0.2" -> "1.0.2"
    $versionStr = $lastTag -replace "^${modName}-v", ""
    if ($versionStr -match '^\d+\.\d+\.\d+$') {
        $parts = $versionStr -split '\.'
        $major = [int]$parts[0]
        $minor = [int]$parts[1]
        $patch = [int]$parts[2]

        # Analyze commit log since last tag in mod's directory
        $commits = git log "$lastTag..HEAD" --oneline -- $modName
        $bump = "patch"
        foreach ($commit in $commits) {
            if ($commit -match '\[major\]|breaking|!') {
                $bump = "major"
                break
            }
            elseif ($commit -match '\[minor\]|feat') {
                $bump = "minor"
            }
        }

        if ($bump -eq "major") {
            $major++
            $minor = 0
            $patch = 0
        }
        elseif ($bump -eq "minor") {
            $minor++
            $patch = 0
        }
        else {
            $patch++
        }

        return "$major.$minor.$patch"
    }

    return $defaultVersion
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

    # Write temporary .csproj
    Set-Content -Path $csprojPath -Value $csprojContent

    try {
        dotnet build $csprojPath -c Release --no-incremental
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to compile mod: $modName"
        }
        Write-Host "Successfully compiled $modName DLL(s)!" -ForegroundColor Green
    }
    finally {
        # Clean up temporary build artifacts
        if (Test-Path $csprojPath) {
            Remove-Item $csprojPath -Force
        }
        $objDir = Join-Path $modDir "obj"
        if (Test-Path $objDir) {
            Remove-Item $objDir -Recurse -Force
        }
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

    foreach ($mod in $mods) {
        $modName = $mod.Name
        $modDir = Join-Path $rootPath $modName
        
        Write-Host "----------------------------------------" -ForegroundColor Gray
        Write-Host "Checking release status for: $modName" -ForegroundColor Yellow

        # Find the latest Git tag for this mod
        $tags = git tag -l "${modName}-v*" --sort=-v:refname | Where-Object { $_ -ne "" }
        $latestTag = $null
        $needsRelease = $false

        if ($null -ne $tags -and $tags.Count -gt 0) {
            $latestTag = $tags[0]
            # Check if there are differences in this mod directory since last tag
            $diffCount = (git diff --name-only $latestTag HEAD -- $modName | Measure-Object).Count
            if ($diffCount -gt 0) {
                Write-Host "Detected $diffCount changed file(s) since last tag ($latestTag)." -ForegroundColor Green
                $needsRelease = $true
            }
            else {
                Write-Host "No changes detected since $latestTag. Skipping release." -ForegroundColor Gray
            }
        }
        else {
            Write-Host "No prior tag found for $modName. Initial release will be created." -ForegroundColor Green
            $needsRelease = $true
        }

        if ($needsRelease) {
            # Determine next version
            $version = Get-NextVersion -modName $modName -lastTag $latestTag -defaultVersion "1.0.0"
            $tag = "${modName}-v$version"
            Write-Host "Target Release Version: $version (Tag: $tag)" -ForegroundColor Cyan

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

            # Update version in the staging copy of About.xml
            $stagingAboutXml = Join-Path $stagingDir "About\About.xml"
            Update-AboutXmlVersion -xmlPath $stagingAboutXml -version $version

            # Compress mod archive
            $zipName = "$modName-$version.zip"
            $zipPath = Join-Path $rootPath $zipName
            if (Test-Path $zipPath) {
                Remove-Item $zipPath -Force
            }
            
            Write-Host "Creating zip package: $zipName" -ForegroundColor Gray
            Compress-Archive -Path $stagingDir -DestinationPath $zipPath -Force

            # Gather commits for release changelog
            if ($null -ne $latestTag) {
                $changelog = git log "$latestTag..HEAD" --oneline -- $modName
            }
            else {
                $changelog = git log --oneline -- $modName
            }

            $notesFile = [System.IO.Path]::GetTempFileName()
            $notesContent = @("### Changes in this release:", "")
            if ($null -ne $changelog -and $changelog.Length -gt 0) {
                foreach ($c in $changelog) {
                    $cleanCommit = $c -replace '^[a-f0-9]+\s+', ''
                    $notesContent += "- $cleanCommit"
                }
            }
            else {
                $notesContent += "- Initial release."
            }
            $notesContent | Out-File -FilePath $notesFile -Encoding utf8

            # Tag repository and push to GitHub
            Write-Host "Tagging repository: $tag" -ForegroundColor Gray
            git tag $tag
            git push origin $tag

            # Create GitHub release and upload zip
            Write-Host "Creating GitHub Release and uploading asset..." -ForegroundColor Gray
            gh release create $tag $zipPath --title "$modName v$version" --notes-file $notesFile
            
            # Clean up temp files
            Remove-Item $notesFile -Force
            Remove-Item $zipPath -Force
            Write-Host "Successfully completed release for $modName v$version!" -ForegroundColor Green
        }
    }

    # Clean up staging parent folder
    if (Test-Path $stagingParent) {
        Remove-Item $stagingParent -Recurse -Force
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Workflow completed successfully." -ForegroundColor Green
[CmdletBinding()]
param(
    [string]$JavaHome,
    [string]$AndroidSdk,
    [string]$Gradle,
    [string]$GradleHome,
    [string]$ArtifactDirectory,
    [switch]$PublicRelease
)

$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$androidProject = Join-Path $projectRoot 'Projects\Android'
$repositoryContainer = Split-Path -Parent $projectRoot
$productVersion = '0.1.0-alpha.3'
$androidVersionCode = 95

function Get-FixedDriveRoots {
    [System.IO.DriveInfo]::GetDrives() |
        Where-Object { $_.IsReady -and $_.DriveType -eq [System.IO.DriveType]::Fixed } |
        ForEach-Object { $_.RootDirectory.FullName }
}

function Select-ExistingPath {
    param([string[]]$Candidates, [string]$Description, [switch]$Directory)
    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if ($Directory) {
            if (Test-Path -LiteralPath $candidate -PathType Container) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        } elseif (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw "$Description was not found. Supply it explicitly when running the script."
}

function Select-CompatibleJavaExecutable {
    param([string[]]$Candidates)
    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate) -or
                -not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            continue
        }
        $versionText = (& $candidate -version 2>&1 | Out-String)
        if ($versionText -match 'version\s+"(?<major>\d+)') {
            $major = [int]$Matches.major
            if ($major -ge 17 -and $major -le 21) {
                return [pscustomobject]@{
                    Executable = (Resolve-Path -LiteralPath $candidate).Path
                    Major = $major
                }
            }
        }
    }
    throw 'A Java 17 through 21 runtime compatible with Gradle 8.5 was not found.'
}

$driveRoots = @(Get-FixedDriveRoots)

if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $repositoryContainer 'artifacts'
}

if ([string]::IsNullOrWhiteSpace($JavaHome)) {
    $javaCandidates = @($env:JAVA_HOME)
    foreach ($driveRoot in $driveRoots) {
        $jdkRoot = Join-Path $driveRoot 'Android\Jdk17'
        if (Test-Path -LiteralPath $jdkRoot -PathType Container) {
            $javaCandidates += Get-ChildItem -LiteralPath $jdkRoot -Directory -ErrorAction SilentlyContinue |
                ForEach-Object { $_.FullName }
        }
    }
    if ($env:ProgramFiles) { $javaCandidates += Join-Path $env:ProgramFiles 'Android\Android Studio\jbr' }
    if ($env:LOCALAPPDATA) { $javaCandidates += Join-Path $env:LOCALAPPDATA 'Programs\Android Studio\jbr' }
    foreach ($driveRoot in $driveRoots) {
        $javaCandidates += Join-Path $driveRoot 'Program Files\Android\Android Studio\jbr'
        $javaCandidates += Join-Path $driveRoot 'Android\Android Studio\jbr'
    }
    $javaSelection = Select-CompatibleJavaExecutable -Candidates ($javaCandidates | ForEach-Object {
        if ($_){ Join-Path $_ 'bin\java.exe' }
    })
    $javaExecutable = $javaSelection.Executable
    $javaMajor = $javaSelection.Major
    $JavaHome = Split-Path -Parent (Split-Path -Parent $javaExecutable)
} else {
    $javaSelection = Select-CompatibleJavaExecutable -Candidates @((Join-Path $JavaHome 'bin\java.exe'))
    $javaExecutable = $javaSelection.Executable
    $javaMajor = $javaSelection.Major
    $JavaHome = Split-Path -Parent (Split-Path -Parent $javaExecutable)
}

if ([string]::IsNullOrWhiteSpace($AndroidSdk)) {
    $sdkCandidates = @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME)
    if ($env:LOCALAPPDATA) { $sdkCandidates += Join-Path $env:LOCALAPPDATA 'Android\Sdk' }
    foreach ($driveRoot in $driveRoots) { $sdkCandidates += Join-Path $driveRoot 'Android\Sdk' }
    $AndroidSdk = Select-ExistingPath -Candidates $sdkCandidates -Description 'The Android SDK' -Directory
} else {
    $AndroidSdk = Select-ExistingPath -Candidates @($AndroidSdk) -Description 'The Android SDK' -Directory
}

if ([string]::IsNullOrWhiteSpace($Gradle)) {
    $gradleCandidates = @()
    foreach ($driveRoot in $driveRoots) {
        $gradleCandidates += Join-Path $driveRoot 'Android\Gradle\gradle-8.5\bin\gradle.bat'
    }
    $Gradle = Select-ExistingPath -Candidates $gradleCandidates -Description 'Gradle 8.5'
} else {
    $Gradle = Select-ExistingPath -Candidates @($Gradle) -Description 'Gradle'
}

if ([string]::IsNullOrWhiteSpace($GradleHome)) {
    if ($env:GRADLE_USER_HOME) {
        $GradleHome = $env:GRADLE_USER_HOME
    }
    if ([string]::IsNullOrWhiteSpace($GradleHome)) {
        $GradleHome = Join-Path $env:LOCALAPPDATA "GalateaDioramaMods\Gradle\java$javaMajor"
    }
}

$sourceApk = Join-Path $androidProject 'build\outputs\apk\release\jkxr-release.apk'
$targetApk = Join-Path $artifactDirectory "JKXR-Diorama-MR-$productVersion.apk"
$localProperties = Join-Path $androidProject 'local.properties'
$hadLocalProperties = Test-Path -LiteralPath $localProperties
$originalLocalProperties = if ($hadLocalProperties) {
    [System.IO.File]::ReadAllBytes($localProperties)
} else {
    $null
}

foreach ($requiredPath in @($javaExecutable, $AndroidSdk, $Gradle, $androidProject)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required dependency not found: $requiredPath"
    }
}

New-Item -ItemType Directory -Path $GradleHome -Force | Out-Null
New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null

$env:JAVA_HOME = $JavaHome
$env:ANDROID_HOME = $AndroidSdk
$env:ANDROID_SDK_ROOT = $AndroidSdk
$env:GRADLE_USER_HOME = $GradleHome

$previousPublicRelease = $env:GALATEA_PUBLIC_RELEASE
if ($PublicRelease) {
    $requiredSigningVariables = @(
        'GALATEA_ANDROID_KEYSTORE',
        'GALATEA_ANDROID_STORE_PASSWORD',
        'GALATEA_ANDROID_KEY_ALIAS',
        'GALATEA_ANDROID_KEY_PASSWORD'
    )
    foreach ($variableName in $requiredSigningVariables) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($variableName))) {
            throw "Public release signing variable is missing: $variableName"
        }
    }

    $releaseKeystore = Select-ExistingPath `
        -Candidates @($env:GALATEA_ANDROID_KEYSTORE) `
        -Description 'The permanent Galatea Android release keystore'
    $developmentKeystore = Join-Path $androidProject 'android.debug.keystore'
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $releaseKeystore).Hash -eq
            (Get-FileHash -Algorithm SHA256 -LiteralPath $developmentKeystore).Hash) {
        throw 'The public release keystore must not be the inherited development keystore.'
    }
}

# CMake/Gradle stores absolute source paths below build\.cxx.  A project moved
# between drives cannot reuse that generated cache, so discard it when its
# metadata points to files that no longer exist.  It will be regenerated by
# the Android build and contains no source files.
$cxxCache = Join-Path $androidProject 'build\.cxx'
$staleCxxCache = $false
if (Test-Path -LiteralPath $cxxCache -PathType Container) {
    $metadataFiles = Get-ChildItem -LiteralPath $cxxCache -Filter 'android_gradle_build.json' -File -Recurse -ErrorAction SilentlyContinue
    foreach ($metadataFile in $metadataFiles) {
        try {
            $metadata = Get-Content -LiteralPath $metadataFile.FullName -Raw | ConvertFrom-Json
            foreach ($buildFile in @($metadata.buildFiles)) {
                if ($buildFile -and -not (Test-Path -LiteralPath ([string]$buildFile))) {
                    $staleCxxCache = $true
                    break
                }
            }
        }
        catch {
            $staleCxxCache = $true
        }

        if ($staleCxxCache) {
            break
        }
    }
}

if ($staleCxxCache) {
    $resolvedCxxCache = (Resolve-Path -LiteralPath $cxxCache).Path
    $expectedCxxCache = [System.IO.Path]::GetFullPath((Join-Path $androidProject 'build\.cxx'))
    if (-not $resolvedCxxCache.Equals($expectedCxxCache, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected CMake cache path: $resolvedCxxCache"
    }
    Write-Host "Removing stale CMake cache: $resolvedCxxCache"
    Remove-Item -LiteralPath $resolvedCxxCache -Recurse -Force
}

# Upstream accidentally tracks a local.properties file from another machine.
# Generate the correct local configuration before Gradle without committing it.
$escapedSdk = $AndroidSdk.Replace('\', '\\').Replace(':', '\:')
@(
    '# Generated by build-diorama-mr.ps1; machine-specific configuration.'
    "sdk.dir=$escapedSdk"
) | Set-Content -LiteralPath $localProperties -Encoding ascii

$locationPushed = $false
try {
    # A normal local build remains convenient for testing, but cannot inherit a
    # stale public-release flag from the parent shell.
    $env:GALATEA_PUBLIC_RELEASE = if ($PublicRelease) { '1' } else { $null }
    Push-Location $androidProject
    $locationPushed = $true
    Write-Host "Java: $JavaHome (major version $javaMajor)"
    Write-Host "Android SDK: $AndroidSdk"
    Write-Host "Gradle: $Gradle"
    Write-Host "Artifacts: $ArtifactDirectory"
    Write-Host "Product version: $productVersion (Android version code $androidVersionCode)"
    Write-Host "Signing mode: $(if ($PublicRelease) { 'permanent public release key' } else { 'development key - not for publication' })"
    & $Gradle assembleRelease --no-daemon
    if ($LASTEXITCODE -ne 0) {
        throw "Gradle exited with code $LASTEXITCODE."
    }
}
finally {
    if ($locationPushed) { Pop-Location }
    $env:GALATEA_PUBLIC_RELEASE = $previousPublicRelease
    if ($hadLocalProperties) {
        [System.IO.File]::WriteAllBytes($localProperties, $originalLocalProperties)
    } elseif (Test-Path -LiteralPath $localProperties) {
        Remove-Item -LiteralPath $localProperties -Force
    }
}

if (-not (Test-Path -LiteralPath $sourceApk)) {
    throw "The build did not produce the expected APK: $sourceApk"
}

New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceApk -Destination $targetApk -Force

$apk = Get-Item -LiteralPath $targetApk
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $targetApk
Write-Host "APK: $($apk.FullName)"
Write-Host "Size: $($apk.Length) bytes"
Write-Host "SHA-256: $($hash.Hash)"

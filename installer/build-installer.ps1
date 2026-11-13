[CmdletBinding()]
param(
    [string]$ApkPath,
    [string]$AndroidSdk,
    [string]$OutputPath,
    [string]$IconPath,
    [switch]$RunSelfTest
)

$ErrorActionPreference = 'Stop'
$sourceDirectory = $PSScriptRoot
$repository = Split-Path -Parent $sourceDirectory
$repositoryContainer = Split-Path -Parent $repository
$artifactDirectory = Join-Path $repositoryContainer 'artifacts'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

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

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $artifactDirectory 'JKXR-Diorama-MR-0.1.0-alpha.4.apk'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $artifactDirectory 'JKXR-Diorama-MR-0.1.0-alpha.4-Installer.exe'
}
if ([string]::IsNullOrWhiteSpace($AndroidSdk)) {
    $sdkCandidates = @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME)
    if ($env:LOCALAPPDATA) { $sdkCandidates += Join-Path $env:LOCALAPPDATA 'Android\Sdk' }
    foreach ($driveRoot in @(Get-FixedDriveRoots)) {
        $sdkCandidates += Join-Path $driveRoot 'Android\Sdk'
    }
    $AndroidSdk = Select-ExistingPath -Candidates $sdkCandidates -Description 'The Android SDK' -Directory
} else {
    $AndroidSdk = Select-ExistingPath -Candidates @($AndroidSdk) -Description 'The Android SDK' -Directory
}
if ([string]::IsNullOrWhiteSpace($IconPath)) {
    $IconPath = Join-Path $sourceDirectory 'assets\galatea-diorama-mods.ico'
}

$platformTools = Join-Path $AndroidSdk 'platform-tools'
$programSource = Join-Path $sourceDirectory 'Program.cs'
$profileSource = Join-Path $sourceDirectory 'ProductProfile.cs'

$required = @(
    $csc,
    $ApkPath,
    (Join-Path $platformTools 'adb.exe'),
    (Join-Path $platformTools 'AdbWinApi.dll'),
    (Join-Path $platformTools 'AdbWinUsbApi.dll'),
    (Join-Path $platformTools 'NOTICE.txt'),
    $programSource,
    $profileSource,
    (Join-Path $sourceDirectory 'app.manifest'),
    $IconPath
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required dependency not found: $path"
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    "/out:$OutputPath",
    "/win32icon:$IconPath",
    "/win32manifest:$(Join-Path $sourceDirectory 'app.manifest')",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.IO.Compression.dll',
    '/reference:System.IO.Compression.FileSystem.dll',
    "/resource:$ApkPath,Installer.Product.apk",
    "/resource:$(Join-Path $platformTools 'adb.exe'),Installer.adb.exe",
    "/resource:$(Join-Path $platformTools 'AdbWinApi.dll'),Installer.AdbWinApi.dll",
    "/resource:$(Join-Path $platformTools 'AdbWinUsbApi.dll'),Installer.AdbWinUsbApi.dll",
    "/resource:$(Join-Path $platformTools 'NOTICE.txt'),Installer.ADB-NOTICE.txt",
    $profileSource,
    $programSource
)

Write-Host "Android SDK: $AndroidSdk"
Write-Host "APK: $ApkPath"
Write-Host "Output: $OutputPath"

& $csc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "The C# compiler exited with code $LASTEXITCODE."
}

$file = Get-Item -LiteralPath $OutputPath
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $OutputPath
Write-Host "Installer: $($file.FullName)"
Write-Host "Size: $($file.Length) bytes"
Write-Host "SHA-256: $($hash.Hash)"

if ($RunSelfTest) {
    $reportPath = Join-Path $env:TEMP (([System.IO.Path]::GetFileNameWithoutExtension($OutputPath)) + '-self-test.txt')
    Remove-Item -LiteralPath $reportPath -Force -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath $file.FullName -ArgumentList @('--self-test', $reportPath) `
        -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath }
        throw "Installer self-test failed with exit code $($process.ExitCode)."
    }
    Get-Content -LiteralPath $reportPath
}

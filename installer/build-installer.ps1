[CmdletBinding()]
param(
    [string]$ApkPath = 'D:\Codex\jkxr-tabletop-mr\artifacts\JKXR-Diorama-MR-0.1.0-alpha.1.apk',
    [string]$AndroidSdk = 'D:\Android\Sdk',
    [string]$OutputPath = 'D:\Codex\jkxr-tabletop-mr\artifacts\JKXR-Diorama-MR-Installer.exe'
)

$ErrorActionPreference = 'Stop'
$sourceDirectory = $PSScriptRoot
$repository = Split-Path -Parent $sourceDirectory
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$platformTools = Join-Path $AndroidSdk 'platform-tools'
$icon = Join-Path $repository 'Projects\Android\jni\OpenJK\code\win32\starwars.ico'

$required = @(
    $csc,
    $ApkPath,
    (Join-Path $platformTools 'adb.exe'),
    (Join-Path $platformTools 'AdbWinApi.dll'),
    (Join-Path $platformTools 'AdbWinUsbApi.dll'),
    (Join-Path $platformTools 'NOTICE.txt'),
    (Join-Path $sourceDirectory 'Program.cs'),
    (Join-Path $sourceDirectory 'app.manifest'),
    $icon
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
    "/win32icon:$icon",
    "/win32manifest:$(Join-Path $sourceDirectory 'app.manifest')",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.IO.Compression.dll',
    '/reference:System.IO.Compression.FileSystem.dll',
    "/resource:$ApkPath,Installer.JKXRDioramaMR.apk",
    "/resource:$(Join-Path $platformTools 'adb.exe'),Installer.adb.exe",
    "/resource:$(Join-Path $platformTools 'AdbWinApi.dll'),Installer.AdbWinApi.dll",
    "/resource:$(Join-Path $platformTools 'AdbWinUsbApi.dll'),Installer.AdbWinUsbApi.dll",
    "/resource:$(Join-Path $platformTools 'NOTICE.txt'),Installer.ADB-NOTICE.txt",
    (Join-Path $sourceDirectory 'Program.cs')
)

& $csc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "The C# compiler exited with code $LASTEXITCODE."
}

$file = Get-Item -LiteralPath $OutputPath
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $OutputPath
Write-Host "Installer: $($file.FullName)"
Write-Host "Size: $($file.Length) bytes"
Write-Host "SHA-256: $($hash.Hash)"

[CmdletBinding()]
param(
    [string]$ApkPath = 'D:\Codex\jkxr-tabletop-mr\artifacts\JKXR-tabletop-v1.2.apk',
    [string]$AndroidSdk = 'D:\Android\Sdk',
    [string]$OutputPath = 'D:\Codex\jkxr-tabletop-mr\artifacts\JKXR-Tabletop-V1.2-Installer.exe'
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
        throw "Dependência não encontrada: $path"
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
    "/resource:$ApkPath,Installer.JKXRTabletop.apk",
    "/resource:$(Join-Path $platformTools 'adb.exe'),Installer.adb.exe",
    "/resource:$(Join-Path $platformTools 'AdbWinApi.dll'),Installer.AdbWinApi.dll",
    "/resource:$(Join-Path $platformTools 'AdbWinUsbApi.dll'),Installer.AdbWinUsbApi.dll",
    "/resource:$(Join-Path $platformTools 'NOTICE.txt'),Installer.ADB-NOTICE.txt",
    (Join-Path $sourceDirectory 'Program.cs')
)

& $csc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "O compilador C# terminou com código $LASTEXITCODE."
}

$file = Get-Item -LiteralPath $OutputPath
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $OutputPath
Write-Host "Instalador: $($file.FullName)"
Write-Host "Tamanho: $($file.Length) bytes"
Write-Host "SHA-256: $($hash.Hash)"

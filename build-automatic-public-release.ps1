[CmdletBinding()]
param(
    [string]$ConfigPath,
    [switch]$Installer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSCommandPath)).TrimEnd('\')
if (-not $ConfigPath) {
    $documents = [Environment]::GetFolderPath("MyDocuments")
    $ConfigPath = Join-Path $documents "Codex\Galatea Private\Android Signing\galatea-signing.json"
}
$resolvedConfigPath = [System.IO.Path]::GetFullPath($ConfigPath)

if (-not (Test-Path -LiteralPath $resolvedConfigPath -PathType Leaf)) {
    throw "Signing configuration not found: $resolvedConfigPath. Run setup-automatic-release-signing.ps1 first."
}
if ($resolvedConfigPath.StartsWith($repoRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The private signing configuration must be stored outside the repository."
}

$config = Get-Content -LiteralPath $resolvedConfigPath -Raw | ConvertFrom-Json
$keystorePath = [System.IO.Path]::GetFullPath([string]$config.KeystorePath)
if (-not (Test-Path -LiteralPath $keystorePath -PathType Leaf)) {
    throw "Signing key not found: $keystorePath"
}

function ConvertTo-PlainText {
    param([Security.SecureString]$SecureValue)
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

$password = ConvertTo-PlainText (ConvertTo-SecureString ([string]$config.ProtectedPassword))
try {
    $env:GALATEA_ANDROID_KEYSTORE = $keystorePath
    $env:GALATEA_ANDROID_STORE_PASSWORD = $password
    $env:GALATEA_ANDROID_KEY_ALIAS = [string]$config.KeyAlias
    $env:GALATEA_ANDROID_KEY_PASSWORD = $password

    & (Join-Path $repoRoot "build-diorama-mr.ps1") -PublicRelease
    if (-not $?) { throw "The public APK build failed." }

    if ($Installer) {
        & (Join-Path $repoRoot "installer\build-installer.ps1")
        if (-not $?) { throw "The APK was built, but the installer build failed." }
    }
}
finally {
    Remove-Item Env:GALATEA_ANDROID_KEYSTORE -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_STORE_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_KEY_ALIAS -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_KEY_PASSWORD -ErrorAction SilentlyContinue
    $password = $null
}

Write-Host "Public release build completed with the permanent Galatea signing key."

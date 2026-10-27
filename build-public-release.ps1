[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KeystorePath,

    [string]$KeyAlias = "galatea-diorama-mr",

    [switch]$Installer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSCommandPath)).TrimEnd('\')
$resolvedKeystorePath = [System.IO.Path]::GetFullPath($KeystorePath)

if (-not (Test-Path -LiteralPath $resolvedKeystorePath -PathType Leaf)) {
    throw "Signing key not found: $resolvedKeystorePath"
}
if ($resolvedKeystorePath.StartsWith($repoRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The permanent signing key must be stored outside the repository."
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

$storePassword = ConvertTo-PlainText (Read-Host "Galatea keystore password" -AsSecureString)
$keyPassword = ConvertTo-PlainText (Read-Host "Galatea key password (usually the same password)" -AsSecureString)

try {
    $env:GALATEA_ANDROID_KEYSTORE = $resolvedKeystorePath
    $env:GALATEA_ANDROID_STORE_PASSWORD = $storePassword
    $env:GALATEA_ANDROID_KEY_ALIAS = $KeyAlias
    $env:GALATEA_ANDROID_KEY_PASSWORD = $keyPassword

    & (Join-Path $repoRoot "build-diorama-mr.ps1") -PublicRelease
    if (-not $?) {
        throw "The public APK build failed."
    }

    if ($Installer) {
        & (Join-Path $repoRoot "installer\build-installer.ps1")
        if (-not $?) {
            throw "The APK was built, but the installer build failed."
        }
    }
}
finally {
    Remove-Item Env:GALATEA_ANDROID_KEYSTORE -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_STORE_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_KEY_ALIAS -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_KEY_PASSWORD -ErrorAction SilentlyContinue
    $storePassword = $null
    $keyPassword = $null
}

Write-Host "Public release build completed with the permanent Galatea signing key."

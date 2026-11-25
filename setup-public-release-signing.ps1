[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KeystorePath,

    [string]$KeyAlias = "galatea-diorama-mr",

    [switch]$Build,

    [switch]$Installer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSCommandPath)).TrimEnd('\')
$resolvedKeystorePath = [System.IO.Path]::GetFullPath($KeystorePath)
$keystoreDirectory = Split-Path -Parent $resolvedKeystorePath

if ($resolvedKeystorePath.StartsWith($repoRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to store the permanent signing key inside the repository. Choose a private path outside: $repoRoot"
}

if (Test-Path -LiteralPath $resolvedKeystorePath) {
    throw "A file already exists at '$resolvedKeystorePath'. This setup never overwrites a signing key."
}
if ($Installer -and -not $Build) {
    throw "Use -Build together with -Installer."
}

$keytoolCandidates = @()
if ($env:JAVA_HOME) {
    $keytoolCandidates += (Join-Path $env:JAVA_HOME "bin\keytool.exe")
}
$pathKeytool = Get-Command keytool.exe -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($pathKeytool) {
    $keytoolCandidates += $pathKeytool.Source
}
$keytoolCandidates += @(
    "C:\Program Files\Android\Android Studio\jbr\bin\keytool.exe"
)

$keytool = $keytoolCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if (-not $keytool) {
    throw "keytool.exe was not found. Install a JDK or set JAVA_HOME, then run this script again."
}

function Read-ConfirmedPassword {
    while ($true) {
        $first = Read-Host "Create the permanent Galatea signing password (minimum 16 characters)" -AsSecureString
        $second = Read-Host "Confirm the permanent Galatea signing password" -AsSecureString

        $firstPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($first)
        $secondPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($second)
        try {
            $firstPlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($firstPtr)
            $secondPlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secondPtr)
        }
        finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($firstPtr)
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secondPtr)
        }

        if ($firstPlain.Length -lt 16) {
            Write-Warning "Use at least 16 characters."
            $firstPlain = $null
            $secondPlain = $null
            continue
        }
        if ($firstPlain -cne $secondPlain) {
            Write-Warning "The passwords did not match."
            $firstPlain = $null
            $secondPlain = $null
            continue
        }

        return $firstPlain
    }
}

New-Item -ItemType Directory -Path $keystoreDirectory -Force | Out-Null
$password = Read-ConfirmedPassword

try {
    $env:GALATEA_ANDROID_KEYSTORE = $resolvedKeystorePath
    $env:GALATEA_ANDROID_STORE_PASSWORD = $password
    $env:GALATEA_ANDROID_KEY_ALIAS = $KeyAlias
    $env:GALATEA_ANDROID_KEY_PASSWORD = $password

    & $keytool -genkeypair `
        -keystore $resolvedKeystorePath `
        -storetype PKCS12 `
        -storepass:env GALATEA_ANDROID_STORE_PASSWORD `
        -alias $KeyAlias `
        -keypass:env GALATEA_ANDROID_KEY_PASSWORD `
        -keyalg RSA `
        -keysize 4096 `
        -sigalg SHA256withRSA `
        -validity 10000 `
        -dname "CN=Galatea Diorama Mods, OU=Galatea Diorama Mods, O=Galatea Interactive"
    if ($LASTEXITCODE -ne 0) {
        throw "keytool failed while creating the signing key."
    }

    Write-Host ""
    Write-Host "Permanent signing certificate:"
    & $keytool -list -v `
        -keystore $resolvedKeystorePath `
        -storepass:env GALATEA_ANDROID_STORE_PASSWORD `
        -alias $KeyAlias |
        Select-String -Pattern "Alias name:|Owner:|Valid from:|SHA256:"
    if ($LASTEXITCODE -ne 0) {
        throw "The new signing key could not be verified."
    }

    if ($Build) {
        & (Join-Path $repoRoot "build-diorama-mr.ps1") -PublicRelease
        if (-not $?) {
            throw "The public APK build failed. The signing key was created successfully and must not be deleted."
        }

        if ($Installer) {
            & (Join-Path $repoRoot "installer\build-installer.ps1")
            if (-not $?) {
                throw "The APK was built, but the installer build failed. The signing key was created successfully and must not be deleted."
            }
        }
    }
}
finally {
    Remove-Item Env:GALATEA_ANDROID_KEYSTORE -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_STORE_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_KEY_ALIAS -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_KEY_PASSWORD -ErrorAction SilentlyContinue
    $password = $null
}

Write-Host ""
Write-Host "Signing key created at: $resolvedKeystorePath"
Write-Host "Back up this file outside this computer and store its password in a password manager before publishing."
Write-Host "Losing either the key or its password prevents in-place updates to published installs."

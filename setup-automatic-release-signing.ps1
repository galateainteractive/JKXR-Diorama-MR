[CmdletBinding()]
param(
    [string]$PrimaryDirectory,
    [string]$BackupDirectory = "F:\Codex\_private\Galatea Android Signing Backup",
    [string]$KeyAlias = "galatea-diorama-mr",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSCommandPath)).TrimEnd('\')
if (-not $PrimaryDirectory) {
    $documents = [Environment]::GetFolderPath("MyDocuments")
    $PrimaryDirectory = Join-Path $documents "Codex\Galatea Private\Android Signing"
}

$primaryPath = [System.IO.Path]::GetFullPath($PrimaryDirectory).TrimEnd('\')
$backupPath = [System.IO.Path]::GetFullPath($BackupDirectory).TrimEnd('\')
$keystorePath = Join-Path $primaryPath "galatea-diorama-release.p12"
$configPath = Join-Path $primaryPath "galatea-signing.json"
$certificatePath = Join-Path $primaryPath "certificate.txt"
$recoveryPath = Join-Path $backupPath "RECOVERY-KEEP-PRIVATE.txt"

foreach ($privatePath in @($primaryPath, $backupPath)) {
    if ($privatePath.StartsWith($repoRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Private signing files cannot be stored inside the repository: $privatePath"
    }
}
foreach ($target in @($keystorePath, $configPath, $recoveryPath)) {
    if (Test-Path -LiteralPath $target) {
        throw "Refusing to overwrite existing signing material: $target"
    }
}

$keytoolCandidates = @()
if ($env:JAVA_HOME) {
    $keytoolCandidates += (Join-Path $env:JAVA_HOME "bin\keytool.exe")
}
$keytoolCandidates += @(
    "F:\Android\Jdk17\jdk-17.0.20.1+1\bin\keytool.exe",
    "C:\Program Files\Android\Android Studio\jbr\bin\keytool.exe"
)
$keytool = $keytoolCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $keytool) {
    throw "keytool.exe was not found. Install a JDK or set JAVA_HOME, then run this script again."
}

$randomBytes = New-Object byte[] 32
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $random.GetBytes($randomBytes) } finally { $random.Dispose() }
$password = -join ($randomBytes | ForEach-Object { $_.ToString("x2") })
$protectedPassword = ConvertTo-SecureString $password -AsPlainText -Force | ConvertFrom-SecureString

New-Item -ItemType Directory -Path $primaryPath -Force | Out-Null
New-Item -ItemType Directory -Path $backupPath -Force | Out-Null

try {
    $env:GALATEA_ANDROID_STORE_PASSWORD = $password
    $env:GALATEA_ANDROID_KEY_PASSWORD = $password

    & $keytool -genkeypair `
        -keystore $keystorePath `
        -storetype PKCS12 `
        -storepass:env GALATEA_ANDROID_STORE_PASSWORD `
        -alias $KeyAlias `
        -keypass:env GALATEA_ANDROID_KEY_PASSWORD `
        -keyalg RSA `
        -keysize 4096 `
        -sigalg SHA256withRSA `
        -validity 10000 `
        -dname "CN=Galatea Diorama Mods, OU=Galatea Diorama Mods, O=Galatea Interactive"
    if ($LASTEXITCODE -ne 0) { throw "keytool failed while creating the signing key." }

    $certificate = & $keytool -list -v `
        -keystore $keystorePath `
        -storepass:env GALATEA_ANDROID_STORE_PASSWORD `
        -alias $KeyAlias 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "The new signing key could not be verified." }

    [ordered]@{
        KeystorePath = $keystorePath
        KeyAlias = $KeyAlias
        ProtectedPassword = $protectedPassword
        CreatedUtc = [DateTime]::UtcNow.ToString("o")
    } | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
    Set-Content -LiteralPath $certificatePath -Value $certificate -Encoding UTF8

    Copy-Item -LiteralPath $keystorePath -Destination (Join-Path $backupPath "galatea-diorama-release.p12")
    Copy-Item -LiteralPath $certificatePath -Destination (Join-Path $backupPath "certificate.txt")
    @(
        "GALATEA DIORAMA MODS - ANDROID SIGNING RECOVERY"
        "Keep this file private. Do not commit or publish it."
        ""
        "Keystore: galatea-diorama-release.p12"
        "Alias: $KeyAlias"
        "Store password: $password"
        "Key password: $password"
        "Created UTC: $([DateTime]::UtcNow.ToString('o'))"
        ""
        "Certificate details:"
        $certificate
    ) | Set-Content -LiteralPath $recoveryPath -Encoding UTF8

    if (-not $SkipBuild) {
        & (Join-Path $repoRoot "build-automatic-public-release.ps1") -ConfigPath $configPath -Installer
        if (-not $?) {
            throw "Signing setup succeeded, but the public build failed. Keep both signing directories."
        }
    }
}
finally {
    Remove-Item Env:GALATEA_ANDROID_STORE_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:GALATEA_ANDROID_KEY_PASSWORD -ErrorAction SilentlyContinue
    [Array]::Clear($randomBytes, 0, $randomBytes.Length)
    $password = $null
}

Write-Host ""
Write-Host "Permanent Galatea signing setup completed."
Write-Host "Primary protected configuration: $primaryPath"
Write-Host "Recovery backup: $backupPath"
Write-Host "Copy the recovery backup to trusted cloud storage or another device before publishing."

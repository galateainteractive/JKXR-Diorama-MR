[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$androidProject = Join-Path $projectRoot 'Projects\Android'
$javaHome = 'C:\Users\User\.jdks\temurin-17.0.20.1'
$androidSdk = 'D:\Android\Sdk'
$gradle = 'D:\Android\Gradle\gradle-8.5\bin\gradle.bat'
$gradleHome = 'D:\Android\CodexGradle\jkxr-gradle-home'
$artifactDirectory = 'D:\Codex\jkxr-tabletop-mr\artifacts'
$sourceApk = Join-Path $androidProject 'build\outputs\apk\release\jkxr-release.apk'
$targetApk = Join-Path $artifactDirectory 'JKXR-tabletop-v2.1.apk'
$localProperties = Join-Path $androidProject 'local.properties'
$hadLocalProperties = Test-Path -LiteralPath $localProperties
$originalLocalProperties = if ($hadLocalProperties) {
    [System.IO.File]::ReadAllBytes($localProperties)
} else {
    $null
}

foreach ($requiredPath in @(
    (Join-Path $javaHome 'bin\java.exe'),
    $androidSdk,
    $gradle,
    $androidProject
)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Dependência não encontrada: $requiredPath"
    }
}

$env:JAVA_HOME = $javaHome
$env:ANDROID_HOME = $androidSdk
$env:ANDROID_SDK_ROOT = $androidSdk
$env:GRADLE_USER_HOME = $gradleHome

# O upstream versiona por engano um local.properties de outra máquina. Gere a
# configuração local correta antes de chamar o Gradle, sem levá-la ao commit.
$escapedSdk = $androidSdk.Replace('\', '\\').Replace(':', '\:')
@(
    '# Gerado por build-tabletop.ps1; configuração específica desta máquina.'
    "sdk.dir=$escapedSdk"
) | Set-Content -LiteralPath $localProperties -Encoding ascii

Push-Location $androidProject
try {
    & $gradle assembleRelease --no-daemon
    if ($LASTEXITCODE -ne 0) {
        throw "O Gradle terminou com código $LASTEXITCODE."
    }
}
finally {
    Pop-Location
    if ($hadLocalProperties) {
        [System.IO.File]::WriteAllBytes($localProperties, $originalLocalProperties)
    } elseif (Test-Path -LiteralPath $localProperties) {
        Remove-Item -LiteralPath $localProperties -Force
    }
}

if (-not (Test-Path -LiteralPath $sourceApk)) {
    throw "O build terminou sem produzir o APK esperado: $sourceApk"
}

New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceApk -Destination $targetApk -Force

$apk = Get-Item -LiteralPath $targetApk
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $targetApk
Write-Host "APK: $($apk.FullName)"
Write-Host "Tamanho: $($apk.Length) bytes"
Write-Host "SHA-256: $($hash.Hash)"

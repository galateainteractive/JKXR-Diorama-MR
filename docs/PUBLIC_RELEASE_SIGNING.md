# Public release signing

Every Android APK must be signed, including APKs distributed directly from the
Galatea website. Public JKXR Diorama MR releases must use the permanent Galatea
key from the first published build onward.

The permanent key and its password must never be committed to Git, copied into
an issue, or included in a release archive.

## Recommended automatic setup

Choose two private locations outside the repository. Prefer a recovery location
on a separate encrypted drive or other protected device:

```powershell
.\setup-automatic-release-signing.ps1 `
  -PrimaryDirectory "$env:USERPROFILE\Documents\Galatea Private\Android Signing" `
  -BackupDirectory "E:\Galatea Signing Recovery"
```

This creates a cryptographically random password without displaying it, stores
the everyday configuration protected by the current Windows account, and
creates a separate recovery backup. It then builds the public APK and installer.
Neither private location is part of the Git repository or release artifacts.
If the directory parameters are omitted, both locations default to separate
folders under the current user's Documents folder; copy the recovery folder to
a different protected device before publishing.

Later public builds require only:

```powershell
.\build-automatic-public-release.ps1 -Installer
```

The recovery backup contains the portable password required after moving to a
different Windows installation. Keep it private and copy that backup to trusted
cloud storage or a separate device before publishing.

## Manual first-time setup

Run the setup script from the repository root and choose a private path outside
the repository:

```powershell
.\setup-public-release-signing.ps1 `
  -KeystorePath "C:\private-backup\galatea-diorama-release.p12" `
  -Build `
  -Installer
```

The script prompts for the password without echoing it, creates a 4096-bit RSA
PKCS#12 signing key, displays its certificate fingerprint, and optionally runs
the public APK and installer builds. It refuses to overwrite an existing key or
place one inside the repository.

Before publishing, keep:

1. The original key in a private location outside the source repository.
2. A second encrypted copy on a different device or trusted cloud backup.
3. The password and key alias in a password manager.
4. A private record of the certificate SHA-256 fingerprint.

The default alias is `galatea-diorama-mr`.

## Later releases

Use the same keystore, alias, and passwords for every update. The helper prompts
for both passwords without echoing them and clears the temporary environment
variables after the build:

```powershell
.\build-public-release.ps1 `
  -KeystorePath "C:\private-backup\galatea-diorama-release.p12" `
  -Installer
```

Internally, the public build uses these temporary environment variables:

- `GALATEA_ANDROID_KEYSTORE`
- `GALATEA_ANDROID_STORE_PASSWORD`
- `GALATEA_ANDROID_KEY_ALIAS`
- `GALATEA_ANDROID_KEY_PASSWORD`

Each published update must keep the same Android package ID and signing key and
must use a higher `versionCode`. Losing the key or its password makes an
in-place update impossible.

The Windows installer is a separate executable. Android signing does not remove
Windows SmartScreen warnings; avoiding those warnings requires an Authenticode
code-signing certificate for the installer.

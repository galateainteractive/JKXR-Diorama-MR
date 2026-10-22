# Galatea Diorama Mods installer — JKXR Diorama MR

The installer is a branded, standalone Windows executable that embeds the
Diorama MR APK and the official Android SDK platform-tools components required
for ADB. It does not contain Jedi Academy game data.

## User workflow

1. Detect Jedi Academy through Steam, GOG, LucasArts, or Windows registry data.
2. Optionally search fixed drives or select any folder manually.
3. Validate `assets0.pk3` through `assets3.pk3` as readable game archives.
4. Detect an authorized Meta Quest over USB/ADB.
5. Install the APK, create `/sdcard/JKXR-Diorama-MR/JK3/base`, and copy the four PK3 files.
6. Compare each local and remote file size, then launch the game.

The folder picker accepts the game root, `GameData`, or `GameData\base`.

## Building

Generate the current APK first, then run:

```powershell
.\installer\build-installer.ps1
```

The installer is compiled with the Windows .NET Framework 4.8 C# compiler, so
it does not require a separate modern .NET runtime installation.

The scripts discover Java, Gradle, and the Android SDK across fixed drives and
write release artifacts to the repository's sibling `artifacts` directory.
Every detected path can still be overridden explicitly:

```powershell
.\installer\build-installer.ps1 `
  -ApkPath X:\path\to\JKXR-Diorama-MR.apk `
  -AndroidSdk X:\Android\Sdk `
  -OutputPath X:\path\to\JKXR-Diorama-MR-Installer.exe
```

## Reusing the Galatea installer shell

The visual shell and installation workflow are intentionally reusable across
Galatea Diorama Mods projects. Product-specific values live in
`ProductProfile.cs`, including the product and game names, Android package,
Quest data paths, Steam app ID, command-line selector, and required game files.

To adapt the installer for another mod:

1. Copy the `installer` directory into that project's repository.
2. Edit only `ProductProfile.cs` for the new game and Android package.
3. Pass the new APK and optional product icon to `build-installer.ps1`.
4. Extend `GameLocator` only if that game needs a storefront-specific lookup
   beyond the existing Steam, uninstall-registry, drive-scan, and folder-picker
   workflow.

## Distribution

- JKXR, OpenJK, and Diorama MR source: see `LICENSE.txt` in the repository root.
- ADB and Android SDK platform-tools: the official notice file is embedded and
  extracted beside ADB in the installer's temporary working directory.
- Commercial PK3 files must never be added to the executable, Git repository,
  or release package.
- The executable is not yet Authenticode-signed and may trigger Microsoft
  SmartScreen when downloaded by another user.

# Windows installer — JKXR Diorama MR

The installer is a standalone Windows executable that embeds the Diorama MR
APK and the official Android SDK platform-tools components required for ADB.
It does not contain Jedi Academy game data.

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

The current build script still needs its APK and output defaults made portable
before public release. Both paths can already be supplied explicitly:

```powershell
.\installer\build-installer.ps1 `
  -ApkPath D:\path\to\JKXR-Diorama-MR.apk `
  -OutputPath D:\path\to\JKXR-Diorama-MR-Installer.exe
```

## Distribution

- JKXR, OpenJK, and Diorama MR source: see `LICENSE.txt` in the repository root.
- ADB and Android SDK platform-tools: the official notice file is embedded and
  extracted beside ADB in the installer's temporary working directory.
- Commercial PK3 files must never be added to the executable, Git repository,
  or release package.
- The executable is not yet Authenticode-signed and may trigger Microsoft
  SmartScreen when downloaded by another user.

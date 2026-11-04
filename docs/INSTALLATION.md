# Installation

## Requirements

- A Meta Quest headset with Developer Mode enabled.
- A Windows PC and a USB data cable for the installer workflow, or another way
  to sideload an APK and copy files with ADB.
- A legally purchased Windows copy of **Star Wars Jedi Knight: Jedi Academy**.
- The four original game archives: `assets0.pk3`, `assets1.pk3`,
  `assets2.pk3`, and `assets3.pk3`.

Diorama MR does not include commercial game data.

## Windows installer

The release installer is intended to provide the simplest setup flow. It:

1. Searches common Steam, GOG, LucasArts, and registry locations.
2. Allows a full disk search or manual folder selection.
3. Accepts the game root, `GameData`, or `GameData\base` folder.
4. Validates the four required PK3 files.
5. Detects an authorized Quest through ADB.
6. Installs the APK and copies the PK3 files to the headset.
7. Verifies the copied file sizes and launches the game.

The installer packages the official Android platform-tools components required
for ADB, together with their notice file. It never packages the commercial PK3
files.

## Manual APK installation

1. Sideload the Diorama MR APK onto the Quest.
2. Start it once, accept the requested storage permission, and then close it.
3. Find the following files in the PC installation under
   `GameData\base`:

   ```text
   assets0.pk3
   assets1.pk3
   assets2.pk3
   assets3.pk3
   ```

4. Copy only those four files to:

   ```text
   /sdcard/JKXR-Diorama-MR/JK3/base
   ```

5. Start **JKXR Diorama MR** from the Quest library's unknown-sources section.

Do not copy the PC executable, DLL files, or the rest of the installation
folder.

## Updating an existing installation

Once the game PK3 files are present on the headset, normal development updates
only require installing the new APK over the existing installation. The full
Windows installer is only needed again if its setup logic changes or the game
data must be recopied.

Diorama MR uses the Android package identifier
`com.galateainteractive.jkxrdioramamr` and keeps its data under
`/sdcard/JKXR-Diorama-MR`. It can therefore coexist with upstream JKXR and its
`/sdcard/JKXR` data. Saves, settings, and game files are not migrated between
the two applications automatically.

Private alpha APKs currently use a development signing key. Before public
distribution, the project still needs a permanent release signing key. An APK
signed with that future key cannot update a development-signed installation;
the application must be uninstalled first. The separate game-data folder is
not removed by a normal Android application uninstall.

## First launch

1. Start Jedi Academy and load a normal gameplay level.
2. Recenter the Quest while looking toward the intended diorama location.
3. Look slightly downward to find the miniature and player character.
4. Move your head sideways to verify 6DoF parallax.
5. Move the character and verify that the diorama remains physically anchored.
6. Use one grip to drag it and both grips to rotate or resize it.
7. Open **Setup > Diorama / VR** to switch modes or open the control guide for
   the active mode.

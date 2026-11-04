# JKXR Diorama MR — Website Release Copy

> Replace the four download URL placeholders with the final website URLs
> before publishing.

## Short description

**JKXR Diorama MR** brings *Star Wars Jedi Knight: Jedi Academy* into a mixed
reality diorama. Play from above, move and rotate the miniature world with both
hands, or switch to the original immersive VR presentation at any time.

Created by **Galatea Diorama Mods**, based on the open-source JKXR project by
Team Beef and its upstream open-source components.

## Alpha notice

This is an early alpha release. Core gameplay is playable, but some levels,
scripted sequences, interactions, visual effects, or performance-heavy areas may
still need additional Diorama MR-specific tuning. Please report reproducible
issues with the level name, save/load context, and the action that triggered the
problem.

## Requirements

- A compatible Meta Quest headset with Developer Mode enabled.
- A Windows PC and a USB connection for installation.
- A legally obtained PC installation of *Star Wars Jedi Knight: Jedi Academy*.
- Permission to use USB debugging when requested by the headset.

The original game and its copyrighted data files are **not included**.

## Which download should I use?

### Windows installer — recommended

Use the installer for a first installation. It can search supported PC game
locations or let you select the *Jedi Academy* folder manually. It validates the
required game data, installs the Quest application, copies the required data,
and launches the game.

Download: `[INSTALLER_URL]`

### APK — manual installation and updates

The APK is provided for advanced users and for updating an existing JKXR
Diorama MR installation. Installing only the APK does not supply the original
game data required for a fresh installation.

Download: `[APK_URL]`

### Corresponding source code

JKXR Diorama MR is distributed under the GNU General Public License v2.0. The
complete corresponding source code for this exact release is available here:

Source archive: `[SOURCE_ARCHIVE_URL]`

GitHub tag:
`https://github.com/galateainteractive/JKXR-Diorama-MR/tree/v0.1.0-alpha.1`

License:
`https://github.com/galateainteractive/JKXR-Diorama-MR/blob/v0.1.0-alpha.1/LICENSE.txt`

## Installation

1. Install *Star Wars Jedi Knight: Jedi Academy* on the Windows PC.
2. Enable Developer Mode on the Meta Quest headset.
3. Connect the headset by USB and accept the USB debugging prompt inside it.
4. Run the JKXR Diorama MR installer.
5. Allow the installer to find the game, or choose its folder manually.
6. Follow the installer status until installation and data transfer complete.
7. Put on the headset and launch **JKXR Diorama MR** from Unknown Sources.
8. Grant storage access if Android requests it on first launch.

Windows may display a SmartScreen warning because the installer is not currently
signed with a commercial Windows Authenticode certificate. Verify the published
SHA-256 checksum before running it.

## Playing in Diorama MR

The first clean launch defaults to **Diorama MR** at the minimum miniature
scale. The game also includes a **VR** mode based on the original JKXR immersive
presentation. Open **Setup > Diorama / VR** to switch between them.

Select **View Diorama MR Controls** on that page for the current controls and
interaction instructions. The legacy **Controls > Help** route remains
available as a secondary path.

Game saves are stored separately from the installer. Installing a correctly
signed update over the existing application is intended to preserve the current
installation and saves. Back up important saves before alpha updates.

## Version

Version: `0.1.0-alpha.1`

Android version code: `93`

Package: `com.galateainteractive.jkxrdioramamr`

Release date: `October 27, 2026`

## File verification

Compare the downloaded files with the SHA-256 values published in
`SHA256SUMS.txt`:

Checksums: `[CHECKSUMS_URL]`

## Credits

- **Galatea Diorama Mods** — Diorama MR design, implementation, adaptations,
  installer presentation, and release packaging.
- **Team Beef** — JKXR and the original standalone/immersive VR implementation.
- **OpenJK and upstream contributors** — engine and related open-source work.

See the source repository for the complete copyright, license, and attribution
notices.

## Disclaimer

This is an unofficial fan-made open-source modification. It is not affiliated
with or endorsed by Lucasfilm, Disney, Electronic Arts, Meta, or the original
game publishers and rights holders. All trademarks and copyrighted game assets
belong to their respective owners. You must supply your own legally obtained
copy of the original game.

## Issue reports

Report issues at
`https://github.com/galateainteractive/JKXR-Diorama-MR/issues` and include:

- headset model and system version;
- installer or APK version;
- level or mission name;
- whether the issue followed death, loading, a cutscene, or switching VR modes;
- exact reproduction steps;
- logs, screenshots, or video when available.


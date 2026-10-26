# Changelog

All notable Diorama MR changes will be documented in this file.

## Unreleased

### Fixed

- Reflowed the in-game Diorama MR control guide into short, explicit lines so
  every instruction remains visible on the headset menu surface.
- Sequenced first-run storage permission before OpenXR startup so the Quest
  permission screens no longer race the mixed-reality session.
- Added automatic passthrough recovery when permission or session transitions
  interrupt its initial creation or activation.

### Added

- Updated the Windows installer to Galatea Diorama Installer engine 1.2.1,
  including automatic Quest USB monitoring with distinct disconnected,
  authorization, offline, and ready states.
- A cancel button for the installer's full fixed-drive game search, preserving
  manual folder selection when scanning large disks takes too long.
- Branded Galatea Diorama Mods installer interface with a reusable product
  profile for future diorama projects.
- Mixed-reality miniature presentation for Jedi Academy on Meta Quest.
- Native passthrough with a transparent sky and clean premultiplied-alpha
  composition.
- Fixed 6DoF world anchor and miniature-scale stereo rendering.
- One-hand drag and two-hand move, rotate, and scale interactions.
- Player-character pivot for two-hand rotation and scaling.
- Observer-to-avatar movement orientation.
- Dominant-hand weapon aiming and off-hand Force aiming.
- Dominant-hand B and off-hand Y hold actions for the weapon and Force radial
  selectors in the default right-handed Diorama MR layout.
- Galatea Diorama Mods attribution in the app credits, mode setup page,
  installer, and project documentation.
- Contextual avatar-centered Use support for panels, triggers, and low scripted
  objects.
- Player-focused visibility, PVS, LOD, shadows, effects, doors, movers, NPCs,
  and server snapshots.
- Stable 2D cutscene presentation.
- In-game switching between Diorama MR and the inherited immersive VR mode.
- Automatic transform and control resets after respawns, loads, scripted
  transitions, and mode changes.
- Windows installer prototype with Steam/GOG detection, manual folder selection,
  ADB installation, PK3 validation, copy verification, and automatic launch.

### Changed

- Standardized the first alpha artifacts as
  `JKXR-Diorama-MR-0.1.0-alpha.1.apk` and
  `JKXR-Diorama-MR-0.1.0-alpha.1-Installer.exe`, retaining Android version
  code `93`.
- Added an explicit public-release signing path that requires an external
  Galatea keystore and refuses the inherited development keystore.
- Restricted the portable Android build to Gradle-compatible Java 17 through
  21 runtimes and isolated its cache by Java version.
- Diorama MR now starts each app launch at the minimum miniature scale while
  keeping the avatar at the standard physical distance; scale adjustments are
  preserved through loads, respawns, and level transitions within the session.

- Made Android and installer build scripts portable across drive letters and
  Windows user profiles by automatically discovering Java, Gradle, the Android
  SDK, and the sibling artifacts directory.
- Assigned the standalone Android package ID
  `com.galateainteractive.jkxrdioramamr` and isolated game data under
  `/sdcard/JKXR-Diorama-MR` so Diorama MR can coexist with upstream JKXR.
- Set the first alpha version to `0.1.0-alpha.1`.
- Moved the presentation selector from Difficulty to a dedicated **MR / VR**
  setup page and added mode-aware in-game control titles and help content.
- Lightsaber attacks in Diorama MR use avatar animation and collision instead
  of tracked-hand velocity.
- Grips are reserved for diorama manipulation in Diorama MR.
- The dominant trigger becomes Use while an interactive target is available.
- Swapped the Diorama MR B/X roles: B now opens pause on tap or the weapon
  selector on hold, while X provides immediate alternate attack and saber
  throw. Immersive VR controls remain unchanged.

### Fixed

- Direction drift after deaths, save loads, map changes, and returning from VR.
- Saber collision and alternate saber throw behavior in the miniature view.
- Force targeting and radial-menu visibility in the external camera.
- Missing foliage, effects, doors, and enemies when the headset was far from
  the player character.
- Multiple scripted panel and bomb interactions.
- A save-load crash involving an NPC bowcaster path.
- Grey or fog-tinted passthrough caused by non-black transparent pixels.

# JKXR Diorama MR

JKXR Diorama MR is an experimental mixed-reality way to play **Star Wars
Jedi Knight: Jedi Academy** on Meta Quest. It presents the game world as a
room-scale miniature while keeping the original gameplay, missions, AI,
physics, collisions, and save system at their normal game scale.

The player controls the on-screen character from outside the scene, walks
around the diorama using headset positional tracking, and can grab, move,
rotate, and resize the world with the Touch controllers. The environment is
rendered over Meta Quest passthrough.

Diorama MR is developed under the **Galatea Diorama Mods** name.

This project is based on [Team Beef's JKXR](https://github.com/Team-Beef-Studios/JKXR),
which in turn is based on [OpenJK](https://github.com/JACoders/OpenJK).

> [!IMPORTANT]
> This repository contains an engine port only. It does not contain or
> distribute any commercial Jedi Academy game data. A legally purchased copy
> of the game is required.

## Project status

Diorama MR is currently in private alpha development. The Jedi Academy
single-player campaign is playable, but the full campaign and special gameplay
states have not yet completed release-level testing.

The same APK also retains the original immersive JKXR mode. Choose
**Diorama MR** or **Immersive VR** under **Setup > MR / VR**. A clean
installation starts in Diorama MR; an existing installation remembers the
last selected mode.

Diorama MR installs as a separate Quest application and stores its game data,
saves, and settings separately from upstream JKXR, so both can coexist on the
same headset.

The Windows installer uses the shared **Galatea Diorama Mods** visual shell,
automatically detects common game-library locations, supports manual folder
selection, installs over ADB, verifies copied game archives, and launches the
Quest application.

## Highlights

- Native Meta Quest passthrough with a transparent game sky.
- Fixed miniature-world anchor with full 6DoF viewing parallax.
- One-hand drag and two-hand move, rotate, and scale gestures.
- Two-hand transformations pivot around the player character.
- Character movement is relative to the physical observer-to-avatar direction.
- Player-focused visibility, effects, NPC, door, and interaction processing.
- Controller-based Force and weapon aiming adapted for the miniature view.
- Stable 2D presentation for cutscenes and scripted cameras.
- Automatic control-frame rebuilding after loads, deaths, respawns, scripted
  transitions, and switches back from immersive VR.

## Documentation

- [Installation](docs/INSTALLATION.md)
- [Diorama MR controls](docs/CONTROLS.md)
- [Configuration and console variables](docs/CONFIGURATION.md)
- [Differences from immersive VR](docs/VR_MODE.md)
- [Known limitations](docs/KNOWN_ISSUES.md)
- [Building and development notes](docs/DEVELOPMENT.md)
- [Release checklist](docs/RELEASE_CHECKLIST.md)
- [Public release signing](docs/PUBLIC_RELEASE_SIGNING.md)
- [Website and download-page copy](docs/WEBSITE_RELEASE_COPY.md)
- [Changelog](CHANGELOG.md)

## Supported game and headset

The current Diorama MR build targets the Jedi Academy single-player campaign
on Meta Quest. Jedi Outcast code remains in the inherited JKXR source tree, but
it is not selected by the current Android package and is not part of the first
Diorama MR release target.

## Credits

The Diorama MR adaptation is developed by **Galatea Diorama Mods**. It is a
modified version of JKXR; the original VR port and its systems were created by
Team Beef, including DrBeef, BaggyG, and Bummser, with additional contributions
credited by the upstream project. JKXR is based on the work of the OpenJK
development team and the original game technology released by Raven Software.

See the [upstream JKXR repository](https://github.com/Team-Beef-Studios/JKXR)
for its complete credits, supported platforms, and original immersive VR
documentation.

## License

Source code is distributed under the GNU General Public License v2.0. See
[LICENSE.txt](LICENSE.txt).

## Disclaimer

This engine modification is not made, distributed, or supported by
Activision Publishing, Raven Software, LucasArts, Lucasfilm, or Disney.
Star Wars, Jedi, Jedi Knight, and related names and marks belong to their
respective owners.

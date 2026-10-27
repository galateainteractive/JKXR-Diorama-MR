# Release checklist

## Repository

- [x] Create the standalone private `JKXR-Diorama-MR` repository.
- [x] Keep Team Beef's JKXR repository configured as the read-only `upstream`
  remote.
- [x] Push the complete descendant history and the Diorama MR development
  branch to the private repository.
- [x] Confirm that no Jedi Academy commercial PK3 files, generated APKs,
  installer binaries, credentials, or release keys are tracked.
- [ ] Decide whether the final public home will be the standalone repository or
  an official GitHub fork before opening the source.

## Application identity

- [x] Select `com.galateainteractive.jkxrdioramamr` as the unique reverse-domain
  Android package identifier.
- [x] Update Gradle, the installer app-ops command, force-stop command, and
  launch command to use that identifier.
- [x] Use `/sdcard/JKXR-Diorama-MR` so Diorama MR and upstream JKXR keep
  separate game data, saves, and settings.
- [x] Create the permanent Android release signing key outside the repository.
- [x] Store a Windows-protected working configuration and a separate portable
  recovery backup outside the repository.
- [ ] Copy the portable recovery backup to trusted cloud storage or a separate
  physical device.
- [x] Set the first public version to `0.1.0-alpha.1`.
- [x] Keep Android `versionCode` at `93` for the first public alpha.

## Branding and documentation

- [x] Rename user-facing Tabletop MR text to Diorama MR.
- [x] Remove development version numbers from the Quest launcher label.
- [x] Publish English installation, controls, configuration, VR comparison,
  development, known-issues, and changelog documents.
- [ ] Create a final application icon, repository banner, and screenshots.
- [ ] Add a controller diagram based on the documented control table.
- [ ] Review Team Beef, OpenJK, Raven Software, and contributor attribution.
- [ ] Review the GPL-2.0 source and binary distribution package.

## Build and installer

- [x] Remove machine-specific absolute paths from the Android and installer
  build scripts.
- [x] Build the private alpha APK with the permanent package identifier and a
  development signing key.
- [x] Build the public APK with the permanent release signing key.
- [x] Update the Windows installer to embed the `0.1.0-alpha.1` APK.
- [x] Use versioned release filenames for both APK and Windows installer.
- [ ] Confirm that Steam, GOG, registry, disk search, and manual folder selection
  all find valid Jedi Academy installations.
- [ ] Confirm that the installer rejects Jedi Outcast and incomplete PK3 sets.
- [x] Run the installer self-test and verify APK and installer SHA-256 hashes.
- [x] Document the expected SmartScreen warning for the unsigned alpha
  installer; Authenticode can be added to a later release.

## Headset test matrix

- [ ] Clean installation with no existing JKXR data.
- [ ] APK update over the previous Diorama MR build.
- [ ] Coexistence with upstream JKXR after the package-ID change.
- [ ] New game, save, load, death, respawn, same-map load, and map transition.
- [ ] Switch Diorama MR to VR and back repeatedly.
- [ ] One-grip drag and two-grip translation, rotation, and scaling.
- [ ] Observer-relative movement from multiple physical sides of the diorama.
- [ ] Lightsaber attacks, saber throw, guns, alternate fire, and Force powers.
- [ ] Weapon and Force radial selectors.
- [ ] Panels, bombs, low usable objects, doors, and scripted triggers.
- [ ] Cutscenes, scripted cameras, turrets, vehicles, and companion NPCs.
- [ ] Large outdoor levels and known high-visibility performance scenes.
- [ ] Passthrough appearance in bright and dark physical rooms.
- [ ] Immersive VR regression pass for inherited JKXR controls.

## Alpha release

- [ ] Freeze a tested commit and tag it.
- [ ] Build APK and installer from the tagged commit.
- [ ] Publish SHA-256 hashes and installation instructions.
- [ ] Publish the corresponding GPL-2.0 source at the same time as the binaries.
- [ ] State clearly that users must supply a legally purchased copy of Jedi
  Academy and that commercial game data is not included.
- [x] Open a structured issue template for level, save, reproduction steps,
  selected mode, headset model, and relevant diagnostic logs.

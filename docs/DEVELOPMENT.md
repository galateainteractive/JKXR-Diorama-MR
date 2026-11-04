# Development notes

## Source lineage

Diorama MR is maintained as a descendant of Team Beef's JKXR repository. Keep
the official repository configured as the `upstream` remote and keep Diorama MR
work on its own branch. This preserves attribution, makes upstream comparison
straightforward, and allows the prepared history to be pushed to a later public
fork without reimplementing the changes.

The private repository should not contain Jedi Academy commercial PK3 files,
permanent release signing keys, local SDK credentials, generated APKs,
installer executables, or ADB device data. The inherited Android debug
keystore is not suitable for release signing.

## Local Android build

From the repository root on the configured Windows development machine:

```powershell
.\build-diorama-mr.ps1
```

The script uses the locally configured JDK, Android SDK, NDK, and Gradle
environment. Paths in the current development script are machine-specific and
must be made portable before treating it as public build documentation.

## Architecture summary

- `cg_tabletop` conditionally enables the miniature presentation while leaving
  the inherited VR path available.
- The visual default is `0.04`, with the anchor initially `1.25 m` forward and
  `0.55 m` below the headset.
- Game physics and simulation remain at their original scale. View transforms,
  stereo separation, controller rays, and rendering focus are converted at the
  presentation boundary.
- Passthrough uses `XR_FB_passthrough`. Transparent background pixels are
  cleared to black with zero alpha for correct premultiplied-alpha composition.
- The miniature transform stores a fixed physical anchor and a game anchor.
  Headset motion changes the external viewpoint without moving the world.
- Movement orientation is recalculated from the physical observer to the
  avatar. Internal Jedi Academy yaw deltas are compensated to keep that basis
  stable across gameplay transitions.
- Visibility, PVS, LOD, shadows, effects, server snapshots, doors, movers, and
  NPC processing use the character as their activity focus instead of the
  external headset camera.
- Force and weapon rays use their respective controller orientations transformed
  into game space, while gameplay traces originate at the avatar.
- Use hints and activation share the same avatar-centered query, including a
  short-volume search for low scripted objects and invisible panel triggers.

## State resets

The tabletop control frame is invalidated and rebuilt after relevant map,
snapshot, spawn, death, save-load, time-rewind, scripted-camera, teleport, and
mode-switch transitions. The native input layer also clears retained command
yaw and movement accumulation before a new transform becomes authoritative.

## Android diagnostics

```powershell
adb logcat -s JKXR-DIR:I *:S
adb logcat -s JKXR-USE:I *:S
```

`JKXR-DIR` follows transform serials, spawn state, reference yaw, and the first
movement after a transition. `JKXR-USE` follows the interaction target, raw
controller event, user command, movement event, and interaction result.

## Release engineering

- Create and securely store the permanent release signing key outside Git.
- Run the release test matrix described in `KNOWN_ISSUES.md` and the project
  release checklist.

The private alpha uses package ID `com.galateainteractive.jkxrdioramamr`, data
root `/sdcard/JKXR-Diorama-MR`, semantic version `0.1.0-alpha.2`, and Android
version code `94`. The
inherited Java namespace remains `com.drbeef.jkxr` so its JNI bindings do not
need to change; Gradle's application ID provides the separate installed-app
identity.

Normal builds use the inherited development key and are not public-release
artifacts. A public build must be requested explicitly:

```powershell
$env:GALATEA_ANDROID_KEYSTORE = 'X:\secure\galatea-diorama-mods.jks'
$env:GALATEA_ANDROID_STORE_PASSWORD = '<secret>'
$env:GALATEA_ANDROID_KEY_ALIAS = 'galatea-diorama-mods'
$env:GALATEA_ANDROID_KEY_PASSWORD = '<secret>'
.\build-diorama-mr.ps1 -PublicRelease
```

The build fails if any signing value is missing or if the supplied keystore is
the inherited development keystore. Never commit the keystore or passwords.

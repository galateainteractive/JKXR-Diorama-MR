# Configuration

Diorama MR can be selected in **Setup > MR / VR** from the main menu or the
in-game setup menu. The same setting can also be changed in the Jedi Academy
console.

## Console variables

```text
cg_tabletop 1
cg_tabletopScale 0.04
cg_tabletopForward 1.25
cg_tabletopDown 0.55
```

| Variable | Default | Valid range | Description |
| --- | ---: | ---: | --- |
| `cg_tabletop` | `1` | `0` or `1` | `1` enables Diorama MR. `0` restores the original immersive VR presentation. |
| `cg_tabletopScale` | `0.04` | `0.01` to `0.20` | Visual size of the world. Smaller values produce a smaller diorama. The default is approximately 1:25 scale. |
| `cg_tabletopForward` | `1.25` | `0.25` to `3.00` metres | Initial physical distance from the headset to the diorama anchor. |
| `cg_tabletopDown` | `0.55` | `0.10` to `1.50` metres | Initial vertical distance below the headset. |

The limits are enforced in code even if another value is entered in the
console. These variables are archived in the game configuration. A clean
installation starts with `cg_tabletop 1`, while an existing installation keeps
the user's last selected mode and scale.

## Repositioning

Recenter the Quest while facing the desired initial diorama location, then load
or resume gameplay. During play, use either grip to move the diorama. A
two-controller gesture changes its position, rotation, and scale.

The control reference is rebuilt automatically after deaths, respawns, save
loads, map changes, scripted camera transitions, large scripted teleports, and
switches from VR back to Diorama MR.

## Diagnostic logs

Direction and transform transitions use the Android log tag `JKXR-DIR`:

```powershell
adb logcat -s JKXR-DIR:I *:S
```

Contextual interaction diagnostics use `JKXR-USE`:

```powershell
adb logcat -s JKXR-USE:I *:S
```

These logs are intended for development and bug reports. They record control
state and game transitions, not commercial game data.

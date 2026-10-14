# Diorama MR and immersive VR

The APK contains two presentation and control modes:

- **Diorama MR** presents the game as a passthrough miniature controlled from
  an external viewpoint.
- **VR** restores the inherited Team Beef first-person immersive experience and
  its original interaction model.

Switch modes under **Setup > Difficulty**. Returning to Diorama MR clears the
immersive VR command yaw and movement accumulator before rebuilding the
miniature anchor, preventing the VR orientation from changing the diorama's
movement basis.

## Control adaptations

| System | Diorama MR | Immersive VR |
| --- | --- | --- |
| Viewpoint | External 6DoF miniature view | First-person tracked view |
| Passthrough | Enabled behind transparent sky | Standard immersive background |
| Grips | Move, rotate, and resize the diorama | Weapon stabilization and radial selectors |
| Weapon selector | Hold X and point with the dominant hand | Inherited JKXR grip-based selector |
| Force selector | Hold Y and point with the off hand | Inherited JKXR grip-based selector |
| Lightsaber attack | Trigger-driven third-person animation and avatar collision | Physical controller velocity and motion controls |
| Force activation | Off-hand trigger | Inherited JKXR buttons and optional motion gestures |
| Weapon aim | Dominant-hand direction transformed through the miniature | Tracked weapon pose |
| Force aim | Off-hand direction transformed through the miniature | Tracked hand pose and supported gestures |
| Use/interact | Dominant stick click or contextual dominant trigger | Inherited JKXR use and proximity gestures |
| Cutscenes | Stable 2D screen | Configurable JKXR cinematic presentation |

## What remains shared

Both modes use the same Jedi Academy gameplay, saves, missions, scripts, AI,
weapons, inventory, and underlying JKXR/OpenJK engine. Switching modes does not
create a separate campaign or save format.

The VR mode is retained from Team Beef's JKXR; Diorama MR changes are designed
to be conditional and should not alter its normal first-person behavior. VR
regression testing is still required before a public release.

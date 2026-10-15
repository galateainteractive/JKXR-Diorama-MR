# Diorama MR controls

The table below assumes the default right-handed JKXR control scheme. In the
left-handed scheme, dominant-hand and off-hand functions are swapped where
appropriate.

The in-game **Controls > Help** page automatically identifies the active mode.
It shows **Diorama MR Controls** and the dedicated diorama guide when
`cg_tabletop` is enabled, or **Immersive VR Controls** and Team Beef's original
control scheme and tutorials when immersive VR is active.

## Gameplay

| Input | Action |
| --- | --- |
| Left stick | Move the character relative to the physical observer-to-avatar direction. Up moves away from the observer, down moves toward the observer, and left/right remain lateral as the observer walks around the diorama. |
| Left stick click | Crouch. The existing JKXR crouch toggle setting still applies. |
| Right stick | Turn the character using the configured snap- or smooth-turn mode. |
| Right stick click | Use panels, bombs, switches, and other interactive objects. |
| A | Jump. |
| X | Alternate attack. With the lightsaber, this preserves saber throw when the move is available. |
| Right trigger | Primary attack or weapon fire. When the hand interaction icon is visible, the trigger becomes contextual Use instead. |
| Left trigger | Use the selected Force power. Keep it held for powers that have a continuous action, such as Grip, Lightning, or Drain. |

The Use icon and the Use action share the same avatar-centered interaction
query. The physical headset does not need to be close to the panel or object.

## Radial selectors and menus

| Input | Action |
| --- | --- |
| Tap B | Open or close the pause menu. |
| Hold B | Open the weapon/item selector. Point with the dominant hand and release B to confirm. |
| Tap Y | Open the datapad. |
| Hold Y | Open the Force-power selector. Point with the off hand and release Y to confirm. |

The hold threshold is 250 milliseconds. The selectors were moved from the
grips because both grips are reserved for manipulating the diorama. The weapon
selector uses the dominant-hand upper face button so the same hand opens and
points at the weapon wheel. In the default right-handed layout, that button is
B; alternate attack moves to X on the off hand. Hand roles are mirrored by the
left-handed control scheme.

## Manipulating the diorama

| Input | Action |
| --- | --- |
| Hold either grip and move that controller | Drag the diorama. |
| Hold both grips and move both controllers | Move the diorama. |
| Hold both grips and twist the line between the controllers | Rotate the diorama around the player character. |
| Hold both grips and move the controllers closer together or farther apart | Resize the diorama around the player character. |

The saved `cg_tabletopScale` value is updated after a two-hand resize. Position
and rotation remain session transforms and are rebuilt when the gameplay state
requires a new anchor.

## Aiming

- The off hand controls the Force targeting ray and Force selector pointer.
- The dominant hand controls weapon aim and the weapon/item selector pointer.
- The targeting rays are transformed into the miniature's game space and begin
  at the avatar for gameplay purposes.
- Physical lightsaber swing attacks are disabled in Diorama MR. Primary attacks
  use the avatar's third-person animation so the visible blade and its collision
  trace remain aligned.

## Cinematics

Cutscenes and scripted cameras use JKXR's stable 2D screen presentation. After
the short accidental-skip protection period, a stick click or controller
trigger can advance or skip a cinematic when the game permits it.

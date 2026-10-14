# Known limitations

Diorama MR is an alpha-stage conversion of a game and VR port that were not
originally designed for an external miniature camera.

- Indoor maps can place walls, ceilings, or other geometry between the physical
  observer and the player character.
- Fog, map-specific clipping, and some full-screen effects may need additional
  treatment in individual levels.
- HUD elements and menus still use JKXR's original 2D compositor layers rather
  than controller-mounted Diorama MR panels.
- Very large outdoor scenes can have isolated performance drops because more
  geometry is visible from the external viewpoint.
- Vehicles, turrets, scripted cameras, unusual Force interactions, and special
  mission mechanics require more campaign-wide testing.
- The current target is Jedi Academy single-player on Meta Quest. Jedi Outcast,
  multiplayer, Pico, and PCVR are not part of the first Diorama MR release
  scope.
- Private alpha builds are not yet signed with the permanent public release
  key. Moving to that key will require uninstalling a development-signed APK;
  the separate game-data folder will remain on the headset.
- The Windows installer is not yet Authenticode-signed and may trigger a
  Microsoft SmartScreen warning.

When reporting a problem, include the level or save location, the selected
mode, the action that triggered it, whether it survives a reload, and relevant
`JKXR-DIR` or `JKXR-USE` log output when available.

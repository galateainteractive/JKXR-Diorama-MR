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
- The LAN co-op mode is experimental, supports exactly two Quest headsets on
  the same local network, and gives each headset an independent diorama anchor.
  Spatial colocation and internet matchmaking are not included.
- Jedi Outcast, competitive multiplayer, Pico, and PCVR are not release targets
  for the current Diorama MR alpha.
- Public releases use the permanent Galatea signing key. Local development and
  co-op test APKs use a different key and cannot update a public installation
  in place (or vice versa); the separate game-data folder remains on the
  headset after a normal application uninstall.
- The Windows installer is not yet Authenticode-signed and may trigger a
  Microsoft SmartScreen warning.

When reporting a problem, include the level or save location, the selected
mode, the action that triggered it, whether it survives a reload, and relevant
`JKXR-DIR` or `JKXR-USE` log output when available.

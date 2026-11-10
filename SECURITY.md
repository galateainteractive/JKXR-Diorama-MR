# Security policy

## Supported version

Security fixes are evaluated for the latest published Diorama MR alpha. Older
alpha builds may be asked to update before a report can be reproduced.

## Reporting a vulnerability

Please do not open a public issue for a suspected security vulnerability. Use
GitHub's private **Report a vulnerability** form in the repository's
**Security** tab:

https://github.com/galateainteractive/JKXR-Diorama-MR/security/advisories/new

Include the affected version, platform, reproduction steps, impact, and a
minimal proof of concept when possible. Do not attach commercial Jedi Academy
game data, signing keys, account credentials, Quest device identifiers, save
files, or unredacted logs containing personal paths.

Ordinary crashes, gameplay bugs, compatibility problems, and feature requests
can be filed through the public issue templates.

## Repository hygiene

Permanent Android release keys and their passwords are kept outside Git. The
tracked `Projects/Android/android.debug.keystore` is an inherited public
development key only; it is not used to sign official releases and must not be
treated as a trusted identity.

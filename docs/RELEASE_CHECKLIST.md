# Casa Miga POS release checklist

## Source
- [ ] Authorized scope completed.
- [ ] Working tree clean and exact SHA recorded.
- [ ] No production or sensitive files tracked.
- [ ] Applied migrations unchanged and schema version documented.

## Automated verification
- [ ] Local `scripts/verify.ps1` passes.
- [ ] GitHub CI passes on the exact commit.
- [ ] Release build has 0 errors / 0 warnings.
- [ ] CodeQL has no unresolved blocking finding.
- [ ] Dependency changes reviewed.
- [ ] Required backup/restore/recovery tests pass.

## Review build
- [ ] Unique build identifier visible.
- [ ] Launcher targets expected executable/DLL.
- [ ] Data directory verified.
- [ ] Build hashes recorded.
- [ ] Isolated data copy used whenever production data must remain untouched.

## Physical/manual smoke when applicable
- [ ] Login / permissions / cash opening.
- [ ] Catalog / POS / checkout / history / reprint.
- [ ] Qian USB print, Variant 04 logo, CP850, partial cut.
- [ ] Cash drawer only when authorized.
- [ ] LAN printing only when authorized.
- [ ] F11/fullscreen and target display.
- [ ] Windows 10 x64 and Windows 11 x64 at installer milestone.
- [ ] Install / upgrade / uninstall / data preservation at installer milestone.

## Release evidence
- [ ] Version, exact SHA, schema, test totals/date.
- [ ] Installer/build SHA-256 when applicable.
- [ ] Exact hardware/build used for manual smoke.
- [ ] Known limitations and rollback/recovery notes.

A GitHub release is a distribution checkpoint, not proof of physical behavior that was not actually tested.

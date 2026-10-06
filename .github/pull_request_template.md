## Scope

Describe the authorized phase/subphase and what changed.

## Verification

- [ ] Change is within authorized scope.
- [ ] No applied migration was edited; schema changes use a new migration.
- [ ] Money, inventory, and history invariants remain traceable.
- [ ] Physical effects occur only after the authoritative commercial commit.
- [ ] No production DB, backup, credentials, terminal config, logs, or customer data were added.
- [ ] `./scripts/verify.ps1` passed locally.
- [ ] Release build has 0 errors and 0 warnings.
- [ ] Diff reviewed.
- [ ] CURRENT_STATE / NEXT_STEPS / evidence updated when applicable.
- [ ] Manual smoke is explicitly marked pending or completed.

## Hardware / manual validation

List required Qian printer, cutter, cash drawer, USB/LAN, fullscreen, installer, Windows 10/11, or cashier-flow validation.

## Final report

Include final HEAD, commits, tests executed/results, migrations/schema impact, data-safety measures, residual risks, and next authorized step.

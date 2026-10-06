# GitHub operating model

GitHub is the remote source of truth for Casa Miga POS code history, review, automated verification, issues, and release evidence. It does not replace physical smoke testing.

## Bootstrap state

The remote repository was created before the complete local tree was uploaded. The canonical local project must be imported from the development PC without reconstructing missing source from README or documentation.

Expected project areas include `src/`, `tests/`, `migrations/`, `docs/`, `research/`, and `scripts/`.

Before the first full push, confirm `.gitignore` excludes production databases, SQLite sidecars, backups, logs, local configuration, pending checkout files, and credentials.

## CI

The Windows CI workflow runs the canonical `scripts/verify.ps1` whenever relevant source/test/build inputs change. Local verification remains mandatory before pushing.

CI success does not prove Qian printer, cutter, cash drawer, USB/LAN transport, display behavior, Windows installer, or cashier workflow.

## Security and dependencies

CodeQL analyzes C# changes. Dependabot tracks NuGet and GitHub Actions updates. Dependency upgrades require regression verification and are never merged merely because they are newer.

## Branches and releases

`main` holds accepted checkpoints. Risky or multi-file work uses focused branches and pull requests.

A GitHub Release may be created only from an explicitly accepted checkpoint and must identify semantic version, exact commit SHA, schema version, automated verification, build/installer hash when applicable, manual/physical smoke status, known limitations, and recovery notes.

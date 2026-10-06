# Canonical local import helper

This folder contains a safety-first helper for importing the **existing canonical local Casa Miga POS Git repository** into the private GitHub repository.

It intentionally does **not**:

- force-push;
- rewrite history;
- merge unrelated histories automatically;
- upload production databases/backups/configuration;
- reconstruct source code from reports.

Run `audit-canonical-import.ps1` from the root of the canonical Casa Miga POS repository on the development PC. It verifies a clean worktree, checks for tracked sensitive/runtime files, confirms required project areas, runs `scripts/verify.ps1`, configures/fetches the GitHub remote safely, and creates an import branch.

After that, history must be reviewed before any final push/merge.

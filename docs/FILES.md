# Incorporaciones 1H.4 — completada y aprobada

Presentation: InventoryView.xaml/.cs, InventoryViewModel.cs, InventoryDisplay.cs; navegación en MainViewModel, MainWindow y PosView. Tests: InventoryViewModelTests.cs. Reporte y aceptación: PHASE_1H4_SMOKE_CORRECTION.md y evidence/phase1h4-smoke-correction.txt. Smoke aprobado; 1H.5 no iniciado.

## Registro anterior

# Incorporaciones 1H.3

- src/CasaMiga.Application/InventoryModels.cs
- src/CasaMiga.Application/InventoryService.cs
- src/CasaMiga.Infrastructure/SqliteInventoryStore.Application.cs
- tests/CasaMiga.Tests/InventoryApplicationTests.cs
- docs/decisions/ADR-016-inventory-application.md
- docs/PHASE_1H3_REPORT.md
- docs/evidence/phase1h3-verification.txt
- docs/evidence/phase1h3-migration-hashes.txt

Actualizados: Inventory Domain, puerto local de autorización, SqliteInventoryStore, SqliteLocalUnitOfWork, pruebas Domain, README, CURRENT_STATE, NEXT_STEPS, database.md y este inventario. Presentation, Checkout, venta, pago, caja, tickets, impresión y migraciones 001–010 no cambiaron.

## Registro anterior

# Incorporaciones 1H.2A

- migrations/010_inventory_receipt_note_fix.sql
- tests/CasaMiga.Tests/InventoryNoteMigrationTests.cs
- docs/PHASE_1H2A_REPORT.md
- docs/evidence/phase1h2a-verification.txt
- docs/evidence/phase1h2a-migration-hashes.txt

Actualizados: cinco tests existentes para esperar esquema 10; README.md, CURRENT_STATE.md, NEXT_STEPS.md, database.md y este inventario. Ningún archivo src modificado.

## Registro anterior
# Inventario de fuentes y documentación — FASE 1H.2

Nuevos en 1H.2:

- migrations/009_inventory_foundation.sql
- src/CasaMiga.Infrastructure/SqliteInventoryStore.cs
- tests/CasaMiga.Tests/InventoryPersistenceTests.cs
- docs/decisions/ADR-015-inventory-ledger-persistence.md
- docs/PHASE_1H2_REPORT.md
- docs/evidence/phase1h2-verification.txt
- docs/evidence/phase1h2-migration-hashes.txt

Actualizados: Inventory.cs, Database.cs, pruebas de migración/identidad, README y documentación persistente. Application, Presentation, Checkout, ventas, caja, tickets e impresión no cambiaron.

## Registro histórico

# Inventario de fuentes y documentación — FASE 1H.1

Nuevos en 1H.1:

- src/CasaMiga.Domain/Inventory.cs
- tests/CasaMiga.Tests/InventoryDomainTests.cs
- docs/decisions/ADR-014-inventory-domain.md
- docs/PHASE_1H1_REPORT.md
- docs/evidence/phase1h1-verification.txt

Actualizados en 1H.1: README.md, docs/CURRENT_STATE.md, docs/NEXT_STEPS.md, docs/domain.md y este inventario. No se modificaron migraciones, Application, Infrastructure ni Presentation.

## Registro histórico

# Inventario de fuentes y documentación — FASE 1G Bloque A

Excluye .git, bin/obj, work, artifacts, TestResults y datos locales.

- .editorconfig
- .gitattributes
- .gitignore
- AGENTS.md
- CasaMigaPOS.slnx
- Directory.Build.props
- docs/architecture.md
- docs/CURRENT_STATE.md
- docs/database.md
- docs/decisions/ADR-001-technology-stack.md
- docs/decisions/ADR-002-database.md
- docs/decisions/ADR-003-money.md
- docs/decisions/ADR-004-inventory-ledger.md
- docs/decisions/ADR-005-hardware-abstraction.md
- docs/decisions/ADR-006-local-identity.md
- docs/decisions/ADR-007-count-and-installation-backup.md
- docs/decisions/ADR-008-catalog-normalization-and-audit.md
- docs/decisions/ADR-009-unhandled-failure-policy.md
- docs/decisions/ADR-010-cash-checkout.md
- docs/decisions/ADR-011-sales-history-read-path.md
- docs/decisions/ADR-012-receipt-snapshot-and-trace.md
- docs/decisions/ADR-013-windows-raw-receipt-transport.md
- docs/dependencies.md
- docs/PHASE_1G_BLOCK_A_REPORT.md
- docs/PHASE_1G_PHYSICAL_CHECKPOINT.md
- docs/printing.example.json
- docs/domain.md
- docs/evidence/dependency-licenses.json
- docs/evidence/maintenance-smoke.txt
- docs/evidence/nuget-audit.json
- docs/evidence/phase1g-block-a-verification.txt
- docs/evidence/phase1a-baseline.txt
- docs/evidence/phase1a-clean.txt
- docs/evidence/phase1a-dependency-licenses.json
- docs/evidence/phase1a-logs.json
- docs/evidence/phase1a-nuget-audit.json
- docs/evidence/phase1a-restart.json
- docs/evidence/phase1a-restore.txt
- docs/evidence/phase1a-source-review.json
- docs/evidence/phase1a-verification.txt
- docs/evidence/phase1b-baseline-clean.txt
- docs/evidence/phase1b-baseline.txt
- docs/evidence/phase1b-clean.txt
- docs/evidence/phase1b-cli.txt
- docs/evidence/phase1b-logs.json
- docs/evidence/phase1b-publish.txt
- docs/evidence/phase1b-restore.txt
- docs/evidence/phase1b-smoke-and-restore.json
- docs/evidence/phase1b-source-review.json
- docs/evidence/phase1b-verification.txt
- docs/evidence/phase1c-baseline-clean.txt
- docs/evidence/phase1c-baseline.txt
- docs/evidence/phase1c-clean.txt
- docs/evidence/phase1c-cli-backup.txt
- docs/evidence/phase1c-cli-restore.txt
- docs/evidence/phase1c-logs.json
- docs/evidence/phase1c-publish.txt
- docs/evidence/phase1c-restore.txt
- docs/evidence/phase1c-smoke-and-restore.json
- docs/evidence/phase1c-smoke.md
- docs/evidence/phase1c-source-review.json
- docs/evidence/phase1c-verification.txt
- docs/evidence/phase1d-baseline-clean.txt
- docs/evidence/phase1d-baseline.txt
- docs/evidence/phase1d-clean.txt
- docs/evidence/phase1d-cli-backup.txt
- docs/evidence/phase1d-cli-restore.txt
- docs/evidence/phase1d-fatal-smoke.json
- docs/evidence/phase1d-logs.json
- docs/evidence/phase1d-publish.txt
- docs/evidence/phase1d-restore.txt
- docs/evidence/phase1d-smoke-and-restore.json
- docs/evidence/phase1d-source-review.json
- docs/evidence/phase1d-verification.txt
- docs/evidence/phase1e-baseline-recovered.txt
- docs/evidence/phase1e-baseline.txt
- docs/evidence/phase1e-checkout.txt
- docs/evidence/phase1e-clean.txt
- docs/evidence/phase1e-cli-backup.txt
- docs/evidence/phase1e-cli-restore.txt
- docs/evidence/phase1e-closing.txt
- docs/evidence/phase1e-domain.txt
- docs/evidence/phase1e-final-clean.txt
- docs/evidence/phase1e-final-migrations.json
- docs/evidence/phase1e-final-publication.json
- docs/evidence/phase1e-final-publish.txt
- docs/evidence/phase1e-final-restore.txt
- docs/evidence/phase1e-final-state.json
- docs/evidence/phase1e-final-verification.txt
- docs/evidence/phase1e-manual-confirmation.md
- docs/evidence/phase1e-persistence.txt
- docs/evidence/phase1e-publication-hashes.json
- docs/evidence/phase1e-publish.txt
- docs/evidence/phase1e-restore-comparison.json
- docs/evidence/phase1e-restore.txt
- docs/evidence/phase1e-service-smoke.txt
- docs/evidence/phase1e-ui-smoke.md
- docs/evidence/phase1e-verification.txt
- docs/evidence/phase1e-visual-clean.txt
- docs/evidence/phase1e-visual-closed.jpg
- docs/evidence/phase1e-visual-count-exact.jpg
- docs/evidence/phase1e-visual-migrations.json
- docs/evidence/phase1e-visual-pending-20260910.jpg
- docs/evidence/phase1e-visual-publication.json
- docs/evidence/phase1e-visual-publish.txt
- docs/evidence/phase1e-visual-restarted-empty.jpg
- docs/evidence/phase1e-visual-restore.txt
- docs/evidence/phase1e-visual-state.json
- docs/evidence/phase1e-visual-verification.txt
- docs/evidence/phase1f-cashier-no-sales.png
- docs/evidence/phase1f-history-detail-payment.png
- docs/evidence/phase1f-history-detail-top.png
- docs/evidence/phase1f-history-exact-folio.png
- docs/evidence/phase1f-history-invalid-folio.png
- docs/evidence/phase1f-history-local-date.png
- docs/evidence/phase1f-history-page1.png
- docs/evidence/phase1f-history-page2.png
- docs/evidence/phase1f-migrations.json
- docs/evidence/phase1f-publication.json
- docs/evidence/phase1f-restore.json
- docs/evidence/phase1f-smoke-state.json
- docs/evidence/phase1f-verification.txt
- docs/evidence/verification.txt
- docs/FILES.md
- docs/NEXT_STEPS.md
- docs/operations.md
- docs/PHASE_0_REPORT.md
- docs/PHASE_1A_PLAN.md
- docs/PHASE_1A_REPORT.md
- docs/PHASE_1B_REPORT.md
- docs/PHASE_1C_REPORT.md
- docs/PHASE_1D_REPORT.md
- docs/PHASE_1E_MANUAL_SMOKE.md
- docs/PHASE_1E_PROGRESS.md
- docs/PHASE_1E_REPORT.md
- docs/PHASE_1E_VISUAL_ACCEPTANCE.md
- docs/PHASE_1F_REPORT.md
- docs/PHASE_1F_SMOKE.md
- docs/roadmap.md
- docs/USER_REQUIREMENTS_PHASE_1A.md
- docs/USER_REQUIREMENTS_PHASE_1B.md
- docs/USER_REQUIREMENTS_PHASE_1C.md
- docs/USER_REQUIREMENTS_PHASE_1D.md
- docs/USER_REQUIREMENTS.md
- global.json
- migrations/001_foundation.sql
- migrations/002_identity_cash.sql
- migrations/003_cash_closing.sql
- migrations/004_catalog.sql
- migrations/005_pos_permission.sql
- migrations/006_cash_sales.sql
- migrations/007_sales_history.sql
- migrations/008_receipt_printing.sql
- NuGet.Config
- README.md
- research/hardware/qian-qop-t80ul-ri-02.md
- research/pos-reference-notes.md
- scripts/publish.ps1
- scripts/verify.ps1
- src/CasaMiga.Application/CasaMiga.Application.csproj
- src/CasaMiga.Application/CashClosingService.cs
- src/CasaMiga.Application/CashService.cs
- src/CasaMiga.Application/CatalogService.cs
- src/CasaMiga.Application/CheckoutService.cs
- src/CasaMiga.Application/IdentityService.cs
- src/CasaMiga.Application/LocalPorts.cs
- src/CasaMiga.Application/packages.lock.json
- src/CasaMiga.Application/Ports.cs
- src/CasaMiga.Application/PosService.cs
- src/CasaMiga.Application/SalesHistoryService.cs
- src/CasaMiga.Application/ReceiptModels.cs
- src/CasaMiga.Application/ReceiptPorts.cs
- src/CasaMiga.Application/ReceiptService.cs
- src/CasaMiga.Application/TextReceiptRenderer.cs
- src/CasaMiga.Domain/Cart.cs
- src/CasaMiga.Domain/CasaMiga.Domain.csproj
- src/CasaMiga.Domain/CashSession.cs
- src/CasaMiga.Domain/Catalog.cs
- src/CasaMiga.Domain/Money.cs
- src/CasaMiga.Domain/packages.lock.json
- src/CasaMiga.Domain/Sale.cs
- src/CasaMiga.Infrastructure/BackupService.cs
- src/CasaMiga.Infrastructure/CasaMiga.Infrastructure.csproj
- src/CasaMiga.Infrastructure/CheckoutJournal.cs
- src/CasaMiga.Infrastructure/Database.cs
- src/CasaMiga.Infrastructure/FoundationRuntime.cs
- src/CasaMiga.Infrastructure/InstallationBackup.cs
- src/CasaMiga.Infrastructure/LocalPasswords.cs
- src/CasaMiga.Infrastructure/packages.lock.json
- src/CasaMiga.Infrastructure/SqliteCatalogStore.cs
- src/CasaMiga.Infrastructure/SqliteLocalUnitOfWork.cs
- src/CasaMiga.Infrastructure/SqliteSaleStore.cs
- src/CasaMiga.Infrastructure/TerminalConfiguration.cs
- src/CasaMiga.Infrastructure/UnhandledFailureDiagnostics.cs
- src/CasaMiga.Infrastructure/EscPosReceiptRenderer.cs
- src/CasaMiga.Infrastructure/PrintingConfiguration.cs
- src/CasaMiga.Infrastructure/SqliteReceiptStore.cs
- src/CasaMiga.Infrastructure/WindowsRawPrintTransport.cs
- src/CasaMiga.Maintenance/CasaMiga.Maintenance.csproj
- src/CasaMiga.Maintenance/packages.lock.json
- src/CasaMiga.Maintenance/Program.cs
- src/CasaMiga.Presentation/App.xaml
- src/CasaMiga.Presentation/App.xaml.cs
- src/CasaMiga.Presentation/Assets/README.md
- src/CasaMiga.Presentation/CasaMiga.Presentation.csproj
- src/CasaMiga.Presentation/CatalogView.xaml
- src/CasaMiga.Presentation/CatalogView.xaml.cs
- src/CasaMiga.Presentation/CatalogViewModel.cs
- src/CasaMiga.Presentation/CheckoutDialog.xaml
- src/CasaMiga.Presentation/CheckoutDialog.xaml.cs
- src/CasaMiga.Presentation/CheckoutViewModel.cs
- src/CasaMiga.Presentation/DiscardDialog.xaml
- src/CasaMiga.Presentation/DiscardDialog.xaml.cs
- src/CasaMiga.Presentation/MainViewModel.cs
- src/CasaMiga.Presentation/MainWindow.xaml
- src/CasaMiga.Presentation/MainWindow.xaml.cs
- src/CasaMiga.Presentation/packages.lock.json
- src/CasaMiga.Presentation/PosView.xaml
- src/CasaMiga.Presentation/PosView.xaml.cs
- src/CasaMiga.Presentation/PosViewModel.cs
- src/CasaMiga.Presentation/Resources/Theme.xaml
- src/CasaMiga.Presentation/SalesHistoryView.xaml
- src/CasaMiga.Presentation/SalesHistoryView.xaml.cs
- src/CasaMiga.Presentation/SalesHistoryViewModel.cs
- tests/CasaMiga.Tests/CartTests.cs
- tests/CasaMiga.Tests/CasaMiga.Tests.csproj
- tests/CasaMiga.Tests/CashClosingTests.cs
- tests/CasaMiga.Tests/CashIdentityTests.cs
- tests/CasaMiga.Tests/CatalogTests.cs
- tests/CasaMiga.Tests/CheckoutTests.cs
- tests/CasaMiga.Tests/FailureDiagnosticsTests.cs
- tests/CasaMiga.Tests/FoundationTests.cs
- tests/CasaMiga.Tests/packages.lock.json
- tests/CasaMiga.Tests/PosTests.cs
- tests/CasaMiga.Tests/SalePersistenceTests.cs
- tests/CasaMiga.Tests/SalesClosingTests.cs
- tests/CasaMiga.Tests/SalesHistoryTests.cs
- tests/CasaMiga.Tests/SaleTests.cs
- tests/CasaMiga.Tests/ReceiptTests.cs

- docs/PHASE_1G_BLOCK_F_REPORT.md
- docs/USER_REQUIREMENTS_PHASE_1G_BLOCK_F.md
- docs/evidence/phase1g-block-f-human-confirmation.md
- docs/evidence/phase1g-block-f-post-smoke.json
- docs/evidence/phase1g-block-f-render-contract.json

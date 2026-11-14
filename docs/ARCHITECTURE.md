# Pharmacy Management System: architecture and delivery plan

## Solution architecture

The Windows client is a locally installed .NET 10 WinForms application. The solution is split into four projects so database access and screens do not own business rules:

```text
PharmacyManagement.Desktop  ->  PharmacyManagement.Application
                            ->  PharmacyManagement.Infrastructure
PharmacyManagement.Infrastructure -> PharmacyManagement.Application + Domain
PharmacyManagement.Application   ->  Domain
```

- **Domain** contains pharmacy records and rules that must hold regardless of the screen or database provider.
- **Application** contains use cases, request/response types, validation boundaries, and service interfaces.
- **Infrastructure** implements persistence, SQLite configuration, backups, printing, and report data access.
- **Desktop** composes the services with dependency injection and provides the WinForms user interface.
- **Database** is SQLite in `%ProgramData%\PharmacyManagement\Database\pharmacy.db`; no server or network connection is needed for normal use.

Critical billing, purchasing, and return use cases will run in SQLite transactions. Stock is held by batch; sales consume eligible batches by earliest expiry (FEFO), with expired batches excluded. Sale lines retain their actual batch cost for profit reporting. Financial transactions are corrected through returns/reversals and audit entries rather than silent deletion. Database upgrades will use EF Core migrations; backups will use SQLite's online backup API.

## Planned database schema

| Table | Purpose and important constraints |
|---|---|
| `Categories` | Unique category names; referenced by medicines with restricted delete. |
| `Medicines` | Catalog, barcode, default prices, minimum stock and active status; unique non-empty barcode. |
| `Suppliers` | Supplier contact and opening balance. |
| `Customers` | Customer profile; nullable on sales to support walk-in customers. |
| `StockBatches` | Medicine, batch number, dates, supplier, cost, price and on-hand quantity; unique medicine/batch and non-negative quantity. |
| `Purchases`, `PurchaseLines` | Supplier invoice totals and immutable line snapshots; invoice number unique. |
| `Sales`, `SaleLines` | Invoice/payment totals and batch-level sold quantity, price, discount and actual unit cost; invoice number unique. |
| `StockMovements` | Append-only stock in/out/adjustment/return history with reference and user. |
| `Expenses` | Dated expense entries for cashflow and P&L. |
| `AppUsers` | Unique username, password hash, role and active status. |
| `LoginHistory` | Login success/failure and timestamp for security review. |
| `AuditLogs` | Sensitive action, user, time, record reference and details. |
| `SupplierPayments`, `CustomerPayments` | Ledger events for supplier balances and customer credit. |
| `SalesReturns`, `SalesReturnLines` | Return reference and original batch/line link; restock disposition explicit. |
| `PurchaseReturns`, `PurchaseReturnLines` | Supplier return reference and returned batch quantities. |
| `PharmacySettings` | Pharmacy identity, currency, invoice, tax/discount, alert, printer and backup preferences. |
| `BackupHistory` | Backup path, timestamp, result and validation details. |

Money uses .NET `decimal` and fixed two-place database precision. Foreign keys are enabled, historical links restrict destructive deletes, and indexes cover invoice dates, barcodes, expiry, batch lookup, usernames, and ledger references. Ledger records are the source of balances; cached balances, if later added, must be reconcilable.

## Folder structure

```text
src/
  PharmacyManagement.Domain/          Entities, value objects, business rules
  PharmacyManagement.Application/     Use cases, DTOs, validation, interfaces
  PharmacyManagement.Infrastructure/  EF Core, SQLite, migrations, backup, printing
  PharmacyManagement.Desktop/         Forms, controls, view models, composition root
docs/                                 Architecture and operations documentation
```

As modules grow, organize each layer by feature (`Medicines`, `Inventory`, `Sales`, `Purchases`, `Reports`, `Security`) rather than placing application logic in form code.

## UI design plan

Use a dark `#14283F` navigation rail, navy `#183153` headings, teal `#159A9C` action accents, a pale `#F4F6F8` workspace, white bordered cards, and restrained success/warning/danger states. Segoe UI, consistent spacing, readable grids, keyboard-first POS, clear validation and confirmations establish the visual system. The shell adapts to window resizing; data-entry screens use explicit labels and field validation. Dashboard figures come from database queries, never sample constants.

The first screen is a live summary dashboard. Navigation entries for unimplemented modules stay disabled until their workflows are connected, avoiding non-functional controls.

## Delivery phases

1. **Project setup and architecture** — solution, dependency boundaries, design system, SQLite bootstrap, initial live dashboard.
2. **Database and models** — complete entities/configuration, EF migrations, indexes and constraints.
3. **Login and user management** — secure password storage, role authorization, login history and audit.
4. **Dashboard and reusable UI** — live operational indicators, trends and alert lists.
5. **Medicines and categories** — validated catalog CRUD and search.
6. **Inventory and batch management** — movements, adjustments, low-stock and expiry lists.
7. **POS and sales** — FEFO checkout, atomic stock deduction, invoice printing and returns.
8. **Purchases and suppliers** — receive batches, supplier ledger/payments and purchase returns.
9. **Customers and expenses** — customer balances/ledger and expense capture.
10. **Reports** — date-filtered operational/financial reports and local export/print.
11. **Backup and restore** — consistent online backups, validation, history and guarded restore.
12. **Settings** — pharmacy, invoice, tax, printer and backup configuration.
13. **Verification and bug fixing** — workflow and integrity checks for the delivered modules.
14. **Installer and deployment** — self-contained Windows publish and installer preserving ProgramData across upgrades/uninstall.

## Current implementation status

The four-project solution and custom WinForms shell now connect to the ProgramData SQLite database through dependency-injected services. The dashboard, medicine/category catalog, inventory receipt and adjustment, stock movement history, POS checkout, purchase receipt, suppliers, customers, expenses, filtered reports with CSV export, pharmacy identity/currency/invoice prefix settings, SQLite backup/restore, first-run administrator setup, login, password changes and user administration have working database-backed workflows.

Sales use a database transaction and FEFO batch allocation; purchase receipt and stock movements are committed together. Cashiers can use the dashboard, medicine lookup and POS. Managers can use inventory, purchasing, supplier/customer, expense and report workflows. Administrators also manage users, settings and backups. Passwords use salted PBKDF2 hashes and sensitive account changes are audited.

Still required for the full production scope: EF migration-based upgrades for schema evolution, sales and purchase returns, customer/supplier payment ledgers, invoice and report printer integration, richer sales charts/alerts, automatic scheduled backups and history, audit/login history screens, tax/discount settings, comprehensive UI validation and accessibility review, and a self-contained installer that preserves ProgramData. The app is not yet a complete pharmacy deployment package.

## Run locally

From the solution directory, run `dotnet run --project src/PharmacyManagement.Desktop/PharmacyManagement.Desktop.csproj`. The first launch creates the local database and asks you to create an administrator account; later launches show the sign-in form. The database remains at `%ProgramData%\PharmacyManagement\Database\pharmacy.db`.

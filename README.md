# PosTPV — Point of Sale (.NET 9 + Blazor)

A restaurant/pizzeria Point of Sale built with **.NET 9**, **Blazor Web App (Interactive Server)**,
**EF Core + SQL Server**, and **SignalR** for real-time kitchen updates. The UI is hand-written CSS
(no Bootstrap/Tailwind) with a token-driven light/dark theme.

> **Status:** Phase 1 — a solid Clean-Architecture foundation with the core POS → Kitchen flow
> working end-to-end. See [Roadmap](#roadmap) for what is intentionally deferred.

## Architecture

Clean Architecture with four layers:

```
src/
  PosTpv.Domain          Entities + enums (no dependencies)
  PosTpv.Application      DTOs, service interfaces + implementations, AutoMapper, FluentValidation,
                          Repository/UnitOfWork contracts
  PosTpv.Infrastructure   EF Core DbContext, configurations, repositories, PBKDF2 hasher, seeding
  PosTpv.Web             Blazor Web App (pages, layouts, components), cookie auth, SignalR hub
tests/
  PosTpv.Tests           Integration test for the full order lifecycle
```

## Prerequisites

- .NET 9 SDK
- Podman (or Docker) for the SQL Server container

## Getting started

```bash
# 1. Start SQL Server (published on host port 14333)
podman compose up -d        # or: docker compose up -d

# 2. Run the app (applies migrations and seeds demo data on first start)
dotnet run --project src/PosTpv.Web --launch-profile http
```

Then open **http://localhost:5209** (HTTPS: https://localhost:7200).

> The connection string uses `127.0.0.1,14333` rather than `localhost` on purpose: on Windows
> `localhost` resolves to IPv6 `::1` first, which the Podman/WSL port-forward does not answer.

### Demo accounts (PIN `1234`)

| Username  | Role    | Sees                                            |
|-----------|---------|-------------------------------------------------|
| `admin`   | Admin   | Everything                                       |
| `waiter`  | Waiter  | POS, Orders, Tables, Reservations                |
| `kitchen` | Kitchen | Kitchen display                                  |
| `cashier` | Cashier | POS, Orders, Reservations, Billing               |

## Features (Phase 1)

- **Auth & roles** — cookie authentication; role-gated navigation and pages.
- **Dashboard** — sales today/month, order counts, occupied tables, reservations, top products.
- **POS** — categories rail → product grid → live order panel; open a table before ordering,
  per-line quantity/comment/remove, VAT breakdown, quick cash/card checkout or a **split-payment**
  dialog (multiple tenders, split-equally helper, live remaining/change, mixed methods). Fast
  **keyboard shortcuts** (press <kbd>?</kbd> in the POS for the list). Products with add-ons open an
  **extras picker** (quantity, extra chips, comment, live line total); extras are priced into the
  line and shown on the order and the kitchen ticket.
- **Kitchen display (KDS)** — real-time tickets over SignalR; advance line status; auto-notifies POS.
- **Tables** — drag-and-drop floor-plan editor (move, resize, rotate, grid-snap, lock per table)
  with status colours and live totals + create/edit/delete. Editing runs client-side in JS for
  smoothness and persists the whole layout on save. **Join/separate tables**: group free tables so a
  large party shares one bill (any member opens the shared order and all are occupied/freed together).
- **Reservations** — per-day list, status workflow, table assignment; reserved tables are only
  freed once the bill is fully paid.
- **Orders** — all open comandas with filters and quick charge.
- **Billing** — invoice list with revenue / VAT / average-ticket summary and date range,
  hand-drawn **charts** (daily-revenue bars + payment-method donut, pure SVG/CSS, no chart library),
  plus one-click **export to CSV, Excel (.xlsx) and PDF** with enterprise formatting (role-gated
  download endpoint; ClosedXML for Excel, QuestPDF for PDF).
- **Products & Categories** — full CRUD with availability toggle and colour coding.
- **Theming** — hand-written CSS, CSS variables, light/dark mode, responsive (PC/tablet/mobile).

## Common commands

```bash
dotnet build                                   # build the solution
dotnet test                                    # run the integration test (needs the DB up)
podman compose up -d / down                    # start / stop SQL Server
podman logs -f postpv-db                       # database logs

# EF Core migrations (dotnet-ef required: dotnet tool install --global dotnet-ef)
dotnet ef migrations add <Name> -p src/PosTpv.Infrastructure -s src/PosTpv.Web -o Persistence/Migrations
dotnet ef database update      -p src/PosTpv.Infrastructure -s src/PosTpv.Web
```

## Ports

| Service            | Port          |
|--------------------|---------------|
| Web (HTTP / HTTPS) | 5209 / 7200   |
| SQL Server         | 14333 → 1433  |

## Roadmap

The demo database seeds ~10 days of paid sales on first run, so the dashboard and billing charts
show data immediately.

All modules from the original specification are implemented. Natural next steps for a real
deployment: multi-tenant/site support, printer/receipt integration, stock control, and a hardened
production auth setup (stronger PIN policy, per-user accounts).

## Notes

- `AutoMapper` currently reports advisory GHSA-rvv3-g6hj-g44x (build warning only). Mapping is
  isolated in `MappingProfile`, so it can be swapped for hand-written mappers if desired.

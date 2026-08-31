# Reporting & Analytics module — status

> **This is a build record from 2026-07-13, kept for its design rationale — not a current
> status page.** Two things below are now out of date:
>
> - It says *"Nothing has been committed."* That work shipped. `lante-reporting-service` is
>   deployed and healthy in `new-erp`, with a `/swagger/reporting` gateway route.
> - It refers to Ocelot. **YARP replaced it** — see `KUBERNETES.md`. Route names carried over.
>
> Still accurate and still the point of this document: **9 of the 15 designed reports exist**,
> which 6 do not and why, and the known gaps below. For current module status across the whole
> platform see `docs/CURRENT-STATE.md`.

Built 2026-07-13, against QSL ERP design doc §12 (15 standard reports). See the full
implementation plan for context: `/home/alchemist/.claude/plans/soft-hatching-candy.md`.

## What's done

New stateless (no DB) `reporting-service` at `packages/microservices/reporting/`, aggregating
9 reports from Finance/Operations/Fleet/Stores/HSE/Compliance by forwarding the caller's JWT:

1. Management Accounts (P&L, Trial Balance, Balance Sheet) — `GET /api/v1/reports/management-accounts`
2. Budget vs Actual Variance — `GET /api/v1/reports/budget-variance`
3. Aged Debtors — `GET /api/v1/reports/aged-debtors`
4. Cash Flow Forecast (13-week rolling) — `GET /api/v1/reports/cash-flow-forecast`
5. Project Profitability — `GET /api/v1/reports/project-profitability`
6. Fleet Cost & Utilisation — `GET /api/v1/reports/fleet-cost-utilisation`
7. Procurement Spend by Supplier & Category — `GET /api/v1/reports/procurement-spend`
8. HSE Incident & TRIR — `GET /api/v1/reports/hse-incidents-trir`
9. Compliance Dashboard — `GET /api/v1/reports/compliance-dashboard`

Wired end-to-end: gateway (`yarp.json`/`yarp.Development.json` — this was written when the
gateway still ran Ocelot; YARP replaced it, and the route names carried over), Helm chart
(`kubernetes/helm-charts/reporting-service`), umbrella chart registration, CI workflow
(`build-reporting-service.yml` + `deploy-to-kubernetes.yml` case), `Deployment/docker-compose.yml`,
and frontend (`src/services/reports.js` + `src/components/reports/*.jsx` (9 tabs) +
`src/pages/reports/ReportsPage.jsx`, routed at `/modules/reports` behind `reports.view`/
`reports.export` permissions — those permission strings already existed in `permissions.js`
before this work started).

Backend builds clean (`dotnet build`, 0 warnings/errors) and its `/health` + Swagger endpoints
were smoke-tested standalone. Frontend builds clean (`npm run build`). **Nothing has been
committed** — all of the above sits as uncommitted working-tree changes pending review.

## What's explicitly NOT done (by design, not oversight)

These 6 of the original 15 reports have no backend module to source data from, so they were
skipped rather than stubbed: **Revenue vs KPI Target by Department, Payroll Summary & Cost
Report, Leave Balance Report, KPI Scorecard Progress (19 roles), Fixed Asset Register &
Depreciation Schedule, Annual Financial Statements (audit-ready)**. Build them once Payroll,
Leave, a generic KPI/Scorecard service, and Fixed Assets exist as real backend modules.

## Known gaps / things to double-check before this ships

- **Aged Debtors bucket mismatch**: finance-service's `debtors/aging` only returns
  Current/1-30/31-60/61+ (4 buckets, open-ended tail). The design doc wants
  0-30/31-60/61-90/90+ (a 90-day split). Surfaced as-is with a note in the UI; a true 90-day
  split would require finance-service to change its aging endpoint, or reporting-service to
  re-derive aging from raw per-invoice data instead of the pre-aggregated endpoint.
- **No end-to-end integration test yet.** Each piece was verified in isolation (backend
  `dotnet build` + standalone health check; frontend `npm run build`; gateway JSON validity;
  docker-compose config validity). Nobody has yet run the full stack together and clicked
  through all 9 report tabs against live seeded data through the gateway. Do this next.
- **Dev-mode gateway port for reporting-service** (now `yarp.Development.json`) was set to
  `localhost:8091`, following the dominant convention used by most other services. This
  differs from finance-service's own dev entry, which points at the container hostname
  (`finance-service-api:8080`) instead of `localhost` — that's finance's own inconsistency,
  not something introduced here; worth a second look if local (non-Docker) dev against
  finance-service ever breaks.
- **Pre-existing `Deployment/docker-compose.yml` corruption, unrelated to this feature but
  fixed while working here**: `finance-service-api` and `store-service-api` blocks had been
  merged/mangled by an earlier bad merge — duplicate host ports (both `8087:8080`, and both
  Postgres containers on `5440:5432`), a missing `depends_on` condition on finance, and
  store's `ConnectionStrings__DefaultConnection` pointing at the wrong database
  (`lante_finance` instead of `lante_stores`). Fixed: store now uses port `8083` (host) /
  `postgres-lante-stores` on `5444` (host); `docker compose config` now exits 0.
- **No `Deployment/.env` file exists on disk** (gitignored, per-developer). `docker compose
  config` will warn about unset `DB_USER`/`JWT_SECRET`/etc. until a developer creates one —
  expected, not a bug.
- Helm: `kubernetes/helm-charts/lante-erp-platform`'s `Chart.lock` and
  `charts/reporting-service-1.0.0.tgz` were **not regenerated** (no `helm` CLI in this
  environment) — the repo's `repackage-helm-charts.yml` CI workflow should produce these on
  next push, same as it does for every other service.

## Next steps
1. Run the full stack (`docker-compose up`) and manually click through all 9 report tabs.
2. Fix the Aged Debtors bucket gap if a true 90-day split is required.
3. Review + commit (nothing has been committed yet — awaiting explicit go-ahead per project
   convention).
4. When Payroll/Leave/KPI-Scorecard/Fixed-Assets modules land, revisit the 6 skipped reports.

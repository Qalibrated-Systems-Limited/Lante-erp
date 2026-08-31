# Lante ERP — Current State

**Snapshot:** 2026-08-18, `main` @ `12df8ed8`
**Stack:** .NET 8 · PostgreSQL 18 (schema-per-tenant) · YARP gateway · React 18 + Vite · k3s + Helm + ArgoCD

## What this document is, and what it is not

This is a **verified structural audit**, not a functional QA pass. Everything marked ✅ or 🔴 below
was confirmed by reading the code, querying the live cluster, or making a request against
production. Where a module's *correctness* has not been exercised — which is most of them, because
12 of 14 services have no tests — this document says so rather than implying it works.

Read it as: *what exists, what is deployed, what is known broken, and what nobody has checked.*

| | Meaning |
|:-:|---|
| ✅ | Shipped and verified to the stated extent |
| 🔧 | Partial — works but with a named gap |
| ⬜ | Not started |
| ⚠️ | Present but weak, or unverified where that matters |
| 🔴 | Known broken |

---

## Platform

| Area | Status | Detail |
|---|:-:|---|
| Deployment | ✅ | 16 Deployments healthy in `new-erp`, 0 restarts. GitOps via ArgoCD from `main` |
| Multi-tenancy | ✅ | Schema-per-tenant. Claim-beats-header ordering, regex-validated schema names, `search_path` reset on every connection open. Adversarially tested, CI-gated |
| Least privilege | ✅ | Separate DDL and runtime DB roles, enforced by `validate_least_privilege_connection.py` |
| **Backups** | 🔧 | Built (#200, PRs 1–5: logical dumps, restore verification, k3s SQLite, secrets snapshot, Velero for uploads, pgBackRest WAL archiving) but **`backups.enabled` still `false`** — not yet live. See `docs/backup-runbook.md` |
| Disaster recovery | 🔧 | Single node, `local-path` volumes, reclaim `Delete`. Backup mechanism exists (above) but its only destination today is a self-hosted MinIO instance on the same box — durable against pod/PVC loss, not node loss. Real offsite (Backblaze B2) not yet provisioned |
| Staging environment | 🔧 | `new-erp-staging` namespace + `lante-erp-staging` ArgoCD Application added (#221), auto-synced from `main` for visibility. Production's Application no longer auto-syncs — promotion is now an explicit `argocd app sync lante-erp`. Still one cluster node, no ApplicationSet/canary yet |
| CI test gating | 🔴 | 1 of 23 workflows runs `dotnet test`. The other 22 build and ship untested |
| Architectural CI gates | ✅ | 4 Python validators (tenant interceptors, onboarding parity, least privilege, infrastructure), each with unit tests |
| Build warnings | ✅ | `TreatWarningsAsErrors` repo-wide as of #191, with real fixes rather than blanket suppression |
| Observability | 🔧 | Serilog + `prometheus-net` in 15 services — but the `new-erp` ServiceMonitors are **scraped by nothing** (the only Prometheus belongs to QaliTrack and excludes this namespace). No distributed tracing |
| Audit log | ⚠️ | Gateway records method/path/status/actor only — cannot answer "what changed". Fire-and-forget, so entries drop silently under load |
| Secrets | ⚠️ | Shell-generated into one flat Secret. No vault, no rotation mechanism |
| Auth | ⚠️ | Works, but a **single symmetric JWT key is shared by all 16 services and the gateway** — so it is also the tenant boundary |
| Scaling | 🔴 | Every service pinned to 1 replica. 16 background workers have no scheduling lock, so 2 replicas would double-fire |

Node has ample headroom — 62 Gi RAM with 24% requested, 16 CPU at 58% requested, 397 G free — so
`replicaCount: 1` is a correctness constraint, not a capacity one.

---

## Services

Scope figures are file counts, indicating size and maturity — not completeness.

| Service | Ctrl | Entities | Migrations | Tests | Status |
|---|--:|--:|--:|--:|---|
| `operations` | 26 | 60 | 59 | 1 | ⚠️ Largest service. Projects, assignments, work orders, calibration certificates, timesheets. **Certificate API is 🔴 unreachable — see #195** |
| `ticketing` | 19 | 25 | 46 | 4 | 🔧 Best-tested service. Tickets, SLAs, workflow, KB, public portal. `AlertService` has a check-then-act race (#196 sibling) |
| `user-service` | 17 | 23 | 27 | 4 | ✅ Control plane — identity, 2FA, Google OAuth, roles, permissions, tenants, provisioning |
| `stores` | 16 | 15 | 16 | 0 | ⚠️ Inventory, movements, GRN, transfers. Untested |
| `fleet-service` | 16 | 17 | 28 | 0 | ⚠️ Trucks, drivers, trips, expenses, materials. Untested |
| `crm` | 15 | 40 | 31 | 0 | ⚠️ Leads, deals, quotations, tenders. Untested. **No gateway swagger route** |
| `finance` | 13 | 12 | 9 | 0 | ⚠️ GL, AP/AR, budgets, bank rec, fixed assets, month-end, statutory. **Zero tests on the general ledger.** Services live in `Infrastructure/Services`, unlike every other service |
| `hr` | 13 | 18 | 38 | 0 | ⚠️ Employees, payroll, leave, attendance, appraisal, recruitment. Untested. **No gateway swagger route** |
| `compliance` | 25 | 23 | 12 | 0 | ⚠️ Returns, resolutions, policies, licences, SOPs, whistleblower, DSRs. Untested |
| `procurement` | 12 | 21 | 24 | 0 | ⚠️ Requisitions, POs, suppliers, 3-way match. Untested. **No gateway swagger route** |
| `hse` | 12 | 11 | 6 | 0 | ⚠️ Incidents, RAMS, PPE, toolbox talks, inspections. Untested |
| `reporting` | 9 | 12 | 12 | 0 | 🔧 Stateless aggregator. **9 of 15 designed reports** — see `reporting-module-status.md` |
| `subcontracts` | 7 | 6 | 4 | 0 | ⚠️ Prequalification, awards, retentions, scorecards. Untested |
| `licensing` | 3 | 2 | 8 | 0 | ✅ Cross-tenant catalog, deliberately not tenant-scoped, guarded by a platform-only permission |

`packages/microservices/technician-service/` contains **no git-tracked files** — it is not a
service. Ticketing's `TechnicianServiceClient` calls `operations`; only the name is stale (#197).

---

## Known defects

### 🔴 Live

- **[#195] Calibration certificates return 404.** Verified in production: `/api/v1/calibration-certificates` → `HTTP 404` while comparable routes → `401`. Two copies of `yarp.json` diverged; the deployed one is missing both routes. The frontend calls them in four places, so `CalibrationCertificatePage.jsx` is dead.
- **[#190/#192/#200] Nothing is backed up.** Zero CronJobs in the cluster.

### 🟠 Latent — fires on a specific trigger

- **[#196] Provisioning grants to a nonexistent role in 7 of 14 services**, including finance and hr. `ProvisioningAppRole` is configured nowhere, so services fall back to hardcoded defaults that disagree; the grant is best-effort and reports success anyway. **Fires on the next tenant onboarded**, leaving that tenant with no runtime DB permissions.
- **Background workers double-fire above 1 replica** — alerts, report schedules and licence-expiry emails all duplicate. Currently masked by `replicaCount: 1`.
- **[#194] `fmt` helpers** render `Kshs NaN` and `Invalid Date` to users; `fmt.num` is not locale-pinned. Money is now fixed at 2dp.

### ⚠️ Accepted / tracked

- **[#193] AutoMapper GHSA-rvv3-g6hj-g44x** — suppressed with a per-service audit justification; the patched release is behind a paid licence. Scoped to the single advisory, so a future advisory still fails the build.
- **[#197]** QaliCore → Lante rename unfinished. **[#199]** 28 `.idea/` files tracked. **[#198]** No reconciliation check for orphaned/duplicated config — the root cause of both #195 and the backup gap.

---

## Frontend

35 page directories, 273 `.jsx` files (~73.5k lines). React 18 + Vite, React Router v6, Context
only — no Redux or data-fetching layer. Tailwind with a navy/gold design system, hand-rolled UI kit.

| Area | Status |
|---|:-:|
| Test tooling | 🔧 vitest added 2026-08-18; 21 tests covering `fmt` only |
| TypeScript | ⬜ None |
| Component tests | ⬜ None |
| E2E | ⬜ None |
| Largest file | ⚠️ `operations/AssignmentDetailPage.jsx`, 4,370 lines |

---

## Regulatory (Kenya)

| Item | Status |
|---|:-:|
| VAT, PAYE, NSSF, SHIF, WHT, P9A/P10 | 🔧 Present across finance and hr; **not verified for correctness** |
| **KRA eTIMS e-invoicing** | ⬜ `StubEtimsProvider.cs` — interface exists, implementation does not. Legally required for VAT-registered customers |
| Record retention | 🔧 5 years established from Tax Procedures Act 2015 §23(1)(c); no backup exists to retain anything yet |
| Multi-currency | ⚠️ Partial and uneven — `CurrencyCode` in 36 files, `ExchangeRate` in 12, `FxRate` in 2. Revaluation and FX gain/loss posting unverified |

---

## Explicitly not verified

Being honest about the boundary of this audit:

- **Functional correctness of any business module.** No one has confirmed that payroll computes
  the right PAYE, that a journal balances end-to-end through the UI, or that the 3-way match
  rejects what it should. With 12 of 14 services untested, code structure is all that has been
  checked.
- **Whether existing tenants are healthy.** #196 affects newly provisioned tenants; existing ones
  were probably repaired by earlier fix commits, but nobody has confirmed it per tenant.
- **The 6 reports not built**, and whether the 9 that exist return correct figures.
- **hr / crm / procurement database provisioning.** Those three databases exist live but are
  absent from the from-scratch `initdb` bootstrap and from `generate-secrets.sh` — so a rebuild
  from scratch would silently omit them.
- **Restore.** No backup has ever been taken, so no restore has ever been attempted.

## Maintaining this document

Update it when a module's status genuinely changes, not on every commit. Two rules keep it
trustworthy: **state how something was verified**, and **when you do not know, write ⚠️ and say
what is unchecked** — a status page that overstates confidence is worse than none, because people
stop checking the things it claims are fine.

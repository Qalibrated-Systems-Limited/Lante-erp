# Lante ERP

A multi-tenant ERP for Kenyan SMEs — calibration and lab operations, field service, finance,
HR and payroll, inventory, procurement, HSE and statutory compliance.

Built as **16 .NET 8 microservices** behind a YARP gateway, with a React SPA front end, on
PostgreSQL with **schema-per-tenant** isolation. Deployed to k3s via Helm and ArgoCD.

Previously white-labelled as *QaliCore*; some internal identifiers still carry the old name
(tracked in #197).

---

## Repository layout

| Path | What's in it |
|---|---|
| `apps/lante_frontend/` | React 18 + Vite SPA (JavaScript, Tailwind) |
| `packages/LanteGateway/` | YARP reverse proxy — auth, CORS, rate limiting, audit log |
| `packages/microservices/` | The 14 backend services, each `Api` / `Core` / `Infrastructure` |
| `kubernetes/` | Helm charts, ArgoCD application, secrets scripts |
| `Deployment/` | docker-compose stack for local/self-hosted running |
| `scripts/ci/` | Python validators that enforce architectural invariants |
| `docs/` | Module status, calibration methodology |

## The services

`user-service` is the control plane — identity, tenants, subscriptions, provisioning. The rest
are business domains: `operations` (projects, work orders, calibration certificates),
`ticketing`, `crm`, `hr`, `finance`, `procurement`, `stores`, `fleet`, `compliance`, `hse`,
`subcontracts`, `reporting`, and `licensing` (a cross-tenant catalog, deliberately not
tenant-scoped).

Full list with ports and domains: **`KUBERNETES.md`**.

## Getting started

```bash
# Backend + databases, locally
cd Deployment && docker compose up -d

# Frontend
cd apps/lante_frontend && npm install && npm run dev   # http://localhost:3000
npm test                                                # vitest
```

Fresh-install steps, DNS/TLS, database roles and `.env` configuration are in
**`Deployment/DEPLOYMENT.md`**. Cluster operations are in **`KUBERNETES.md`**.

## Things to understand before changing anything

**Multi-tenancy is schema-per-tenant.** Each tenant gets a `tenant_<slug>` Postgres schema in
each subscribed service's database. A per-connection interceptor sets `search_path` from the
JWT `schema` claim, so a query that forgets a tenant filter returns *your own* tenant's rows
rather than someone else's. Two properties keep that true, and both are enforced by
`scripts/ci/validate_tenant_interceptors.py`: the JWT claim always beats the
`X-Tenant-Schema` header, and `search_path` is set on *every* connection open because pooled
connections retain session state.

**`main` deploys to production.** ArgoCD watches `kubernetes/helm-charts/lante-erp-platform`
with `selfHeal: true` and `prune: true`. There is no staging environment and no human gate.
Work on branches.

**Anything outside ArgoCD's watched path is never deployed.** This has bitten twice — an
orphaned backup CronJob (nothing was ever backed up) and a duplicated `yarp.json` that
diverged from the deployed copy (a shipped feature returning 404). If you add a manifest,
confirm something actually applies it.

**Editing a service subchart template requires repackaging** — `helm package <svc> -d
lante-erp-platform/charts/ --version 1.0.0`. Skipping it changes nothing while appearing to
work. Templates directly under `lante-erp-platform/templates/` do not need this.

## Documentation

| Document | Covers |
|---|---|
| [`KUBERNETES.md`](KUBERNETES.md) | Cluster operations, services, gateway config, troubleshooting |
| [`Deployment/DEPLOYMENT.md`](Deployment/DEPLOYMENT.md) | Fresh install, DNS/TLS, DB roles, backups |
| [`docs/CURRENT-STATE.md`](docs/CURRENT-STATE.md) | Module-by-module status: shipped, partial, missing |
| [`docs/calibration-methodology.md`](docs/calibration-methodology.md) | Calibration domain reference |
| [`packages/microservices/operations/CALIBRATION_UNCERTAINTY.md`](packages/microservices/operations/CALIBRATION_UNCERTAINTY.md) | Measurement uncertainty maths |
| [`docs/reporting-module-status.md`](docs/reporting-module-status.md) | Reporting build record (9 of 15 reports) |
| [`apps/lante_frontend/PORTING_GUIDE.md`](apps/lante_frontend/PORTING_GUIDE.md) | Frontend design system and module porting |

Open work is tracked in GitHub issues, labelled by tier (`tier-0` backup/DR, `tier-1`
security and integrity, `tier-2` quality, `hygiene`).

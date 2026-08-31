# Lante ERP — Deployment Guide

Operational guide for standing up a **fresh** Lante deployment. Lante is a
multi-tenant ERP using **schema-per-tenant** isolation (one Postgres schema per company
per service), a set of .NET 8 microservices behind a **YARP** API gateway, and a React
(Vite) frontend.

> **Scope:** this guide covers the **docker-compose** topology (one Postgres container per
> service). The production Kubernetes deployment is different in one important way — it runs
> a **single** `lante-postgresql` instance with one database per service. See
> `KUBERNETES.md`. Do not carry backup or connection assumptions between the two.

> Previously white-labelled as “QaliCore”. Container names now use `lante-*` throughout;
> the Postgres runtime role (`qalicore_app`, see below) is the one identifier still
> pending, deliberately — it is a live privilege migration, not a find-and-replace. See #197.
>
> ⚠️ **Correction:** an earlier version of this note claimed the Postgres runtime role had
> "already been renamed to `lante_app`". **That is false.** The live least-privilege runtime
> role is **`qalicore_app`** — created by `init/create-app-role.sh:21` and referenced by
> `kubernetes/secrets/generate-secrets.sh:24` (`APP_DB_USER`). No commit has ever renamed it.
>
> That incorrect sentence appears to have caused a real bug: 7 of 14 services now default to
> granting a nonexistent `lante_app` on newly provisioned tenant schemas, and because the grant
> is best-effort it reports success while leaving the tenant with no runtime permissions. See
> **#196**. Renaming the role for real is a privilege migration, not a find-and-replace — see
> **#197**.

---

## 1. Architecture at a glance

| Component | Container | Internal port | Host port (dev) | Database |
|---|---|---|---|---|
| Component | Container | Internal port | Database (host port) |
|---|---|---|---|
| User / Identity / Control-plane | `lante-user-service` | 8080 | `lante_userservice` (5435) |
| Licensing | `lante-license-service` | 8080 | `lante_licensing` (5436) |
| Ticketing | `lante-ticketing-service` | 8080 | `lante_ticketing` (5437) |
| Operations | `lante-operations-service` | 8080 | `lante_operations` (5438) |
| Fleet | `lante-fleet-service` | 8080 | `lante_fleetservice` (5439) |
| Finance | `lante-finance-service` | 8080 | `lante_finance` (5440) |
| Compliance | `lante-compliance-service` | 8080 | `lante_compliance` (5441) |
| HSE | `lante-hse-service` | 8080 | `lante_hse` (5442) |
| Subcontracts | `lante-subcontracts-service` | 8080 | `lante_subcontracts` (5443) |
| Stores | `lante-store-service` | 8080 | `lante_stores` (5444) |
| Reporting | `lante-reporting-service` | 8080 | `lante_reporting` (5445) |
| CRM | `lante-crm-service` | 8080 | `lante_crm` (5446) |
| Procurement | `lante-procurement-service` | 8080 | `lante_procurement` (5448) |
| HR | `lante-hr-service` | 8080 | `lante_hr` (5449) |
| API Gateway (**YARP**) | `lante-gateway` | 5000 | — |
| Redis | `lante-redis` | 6379 | — |
| Frontend (Vite/React) | built separately | — | 3000 (dev) |

Note 5447 is skipped; the port range is not contiguous.

The gateway loads `yarp.DockerCompose.json` under compose (compose service hostnames)
rather than the base `yarp.json`, which carries Kubernetes service hostnames. Local
`dotnet run` uses `yarp.Development.json`.

- In **compose**, each service has its **own Postgres container** — 14 of them, no shared DB.
  In **Kubernetes** it is the opposite: one `lante-postgresql` instance, one database per
  service. This is the single biggest difference between the two topologies.
- The **user-service is the control plane**: its `public` schema holds `Tenants`,
  `SubscriptionPlans`, `CompanySubscriptions`, the global `Permissions` catalog,
  `TenantServiceSchemas` (per-service provisioning tracker), and platform admins.
- Every company gets a `tenant_<slug>` schema **in each subscribed service DB**.
- Two DB roles per service DB:
  - **superuser** (`lante_user`) — used for migrations + provisioning (DDL).
  - **`lante_app`** — least-privilege runtime role the **business services** connect
    as for normal queries. Auto-created on first DB init (see §5).

### Request routing (schema resolution)

> **Superseded (2026-07): single-login.** This flow previously began with the frontend
> sending `X-Tenant-Subdomain: <slug>` derived from the host. It no longer does — see
> `apps/lante_frontend/src/api/axios.js:15-17`. Everyone signs in at one host and the tenant
> is resolved server-side from the user's email (their invite-created directory row in
> `public.Users`). The backend still *accepts* `X-Tenant-Subdomain` in a few places, but
> nothing sends it.

1. The user signs in at the single frontend host. No tenant hint is sent before login.
2. user-service looks up the email in the control-plane `public.Users` directory, resolves
   tenant → schema, authenticates against `tenant_<slug>`, and issues a JWT carrying a
   `schema` claim.
3. The gateway injects `X-Tenant-Schema` from the JWT `schema` claim on downstream calls.
4. Each service's `TenantDbConnectionInterceptor` runs `SET search_path` to that schema on
   every connection open.

Two properties of step 4 worth knowing before you change anything there:

- The `schema` **claim always wins** over the `X-Tenant-Schema` header. The header is honoured
  only when the caller presents a valid `X-Internal-Key`, because anonymous callers can set
  arbitrary headers on their own requests.
- `search_path` is set on **every** connection open, resetting to `public` when there is no
  tenant. Skipping it would leak the previous caller's schema, because pooled connections
  retain session state.

`scripts/ci/validate_tenant_interceptors.py` fails the build if either property regresses.

---

## 2. What changed (since the previous “Lante” deployment)

This deployment is a significant architecture change from the old single-tenant / RLS model:

- **Multitenancy: RLS → schema-per-tenant.** Row-Level-Security and the `app.current_tenant`
  setting are gone. Isolation is now per-Postgres-schema via `search_path`.
- **New runtime role `lante_app`.** Business services connect as this least-privilege
  role at runtime; superuser is only used for migrations/provisioning. **Must exist before
  business services start** — now auto-created via `init/create-app-role.sh` (§5).
- **Provisioning engine.** Creating a company (`POST /platform/companies`) now
  **auto-provisions** all subscribed service schemas in one step (CREATE SCHEMA → migrate →
  seed → grant). No manual step. Re-provision is available on the company detail page.
- **Tenant seeding.** Each new company is seeded with **18 roles** (Admin/MD/Executive locked;
  Finance/HR/Technical/Fleet/ICT/R&D/Sales Manager+Staff editable; + Employee), **9 departments**,
  role→permission defaults, and module reference data (ticketing categories/SLAs/tags, fleet
  trip-types/license-classes).
- ~~**Subdomain-based routing.** `support.<domain>` = platform portal; `<slug>.<domain>` =
  tenant app. Requires wildcard DNS + TLS (§4).~~ **Superseded (2026-07) by single-login.**
  Everyone signs in at one host and the tenant is resolved server-side from the user's email.
  The production Kubernetes ingress serves exactly two hosts — `lante.africa` and
  `kmk.support.qalibrated.co.ke` — with **no wildcard**. `TenantSlug.cs` still documents slugs
  as subdomains and reserves hostnames like `www`/`api`/`admin`, which remains sensible if
  per-tenant hosts are reintroduced later, but it is not how requests are routed today.
- **Gateway CORS** reflects the request `Origin` and still allows `X-Tenant-Subdomain` /
  `X-Tenant-Schema` headers. `X-Tenant-Schema` is load-bearing for internal service-to-service
  calls; `X-Tenant-Subdomain` is vestigial — the frontend no longer sends it.
- **Auth is schema-aware end-to-end** — login, first-login password change, 2FA, Google login
  all resolve the tenant schema. Login against an unprovisioned tenant returns a clean 400.
- **Config extracted to `.env`.** `docker-compose.yml` no longer hardcodes secrets; everything
  comes from `Deployment/.env` via `${VAR}` interpolation.
- **Soft delete retained.** Deleting a company in the UI sets `IsDeleted=true` (restorable). It
  does **not** drop schemas or free the slug/email — that is intentional (see §11).

---

## 3. Prerequisites

- Docker Engine 24+ and Docker Compose v2
- A host with ≥ 4 vCPU / 8 GB RAM for the full stack
- DNS control over your base domain (for wildcard + subdomains)
- A TLS solution supporting **wildcard** certs (e.g. cert-manager / Let’s Encrypt **DNS-01**)
- An SMTP account (welcome emails, notifications)

---

## 4. DNS & TLS

Lante is subdomain-routed. For base domain `qalibrated.co.ke`:

| Host | Purpose |
|---|---|
| `qalibrated.co.ke` (apex) | Marketing site (outside this stack; wildcard does NOT cover the apex) |
| `lante.africa` | **Platform / super-admin portal** |
| `*.qalibrated.co.ke` | **Tenant apps** — each company at `<slug>.qalibrated.co.ke` |

Reserved subdomains (cannot be used as a company slug): `www, support, api, app, admin, mail`.

**Required:**
1. Wildcard DNS `*.qalibrated.co.ke` → your ingress/reverse proxy.
2. Wildcard TLS certificate for `*.qalibrated.co.ke` (DNS-01 challenge — HTTP-01 cannot issue wildcards).
3. `lante.africa` needs its own DNS A/AAAA record and TLS cert — it's outside the `*.qalibrated.co.ke` wildcard.

---

## 5. Database roles (`lante_app`)

Business services connect at runtime as `lante_app`. It is **auto-created** on first DB
initialisation by `init/create-app-role.sh` (mounted into every Postgres container’s
`docker-entrypoint-initdb.d`), using `APP_DB_PASSWORD` from `.env`.

- This runs **only on an empty data volume** (fresh deploy). For existing volumes the role is
  assumed to already exist.
- Per-tenant schema privileges (USAGE/SELECT/INSERT/UPDATE/DELETE) are granted **automatically
  during provisioning**; the init script only creates the role + baseline CONNECT/USAGE.
- If you deploy onto **pre-existing** volumes without the role, create it manually per service DB:
  ```sql
  CREATE ROLE lante_app LOGIN PASSWORD '<APP_DB_PASSWORD>';
  GRANT CONNECT ON DATABASE <db> TO lante_app;
  GRANT USAGE ON SCHEMA public TO lante_app;
  ```

---

## 6. Configuration (`.env`)

All config lives in **`Deployment/.env`** (see the shipped file for the full list). Rotate
everything marked `# ROTATE` before going live:

| Variable | Meaning |
|---|---|
| `DB_USER` / `DB_PASSWORD` | Postgres superuser (migrations/provisioning) |
| `APP_DB_USER` / `APP_DB_PASSWORD` | Runtime least-privilege role (`lante_app`) |
| `JWT_SECRET` | HMAC signing key — **must be ≥ 32 chars and identical across all services** |
| `JWT_ISSUER` / `JWT_AUDIENCE` | Keep as `LanteUserService` unless you re-issue all tokens |
| `JWT_EXPIRY_MINUTES` | Access-token lifetime |
| `REDIS_PASSWORD` | Redis auth |
| `INTERNAL_SERVICE_KEY` | Shared secret guarding internal `/provision` endpoints |
| `SMTP_*` | Outbound email |
| `SMS_ENABLED` | `true`/`false` — master toggle for outbound SMS |
| `TIARA_BASE_URL` | Tiara Connect send-SMS endpoint (`https://api2.tiaraconnect.io/api/messaging/sendsms`) |
| `TIARA_API_KEY` | Tiara Connect API key — **rotate if ever exposed** |
| `TIARA_SENDER_ID` | Tiara Connect registered Sender ID (use the TRANSACTIONAL one, not a PROMOTIONAL one) |
| `LICENSE_PRIVATE_KEY_JWK` | EC P-256 signing key for the licensing service — **generate fresh** |
| `BASE_DOMAIN` | Public base domain (reference) |

> **Never commit a production `.env`.** Add `Deployment/.env` to `.gitignore` and inject
> secrets from your secret manager / CI in production.

**Frontend** config is separate — `apps/lante_frontend/.env`:
- `VITE_API_URL` → the public gateway URL (e.g. `https://api.qalibrated.co.ke` or your proxy path)
- `VITE_BASE_DOMAIN` → `qalibrated.co.ke` (so the app parses `<slug>.qalibrated.co.ke` correctly)

---

## 7. Reverse proxy / ingress

Only expose the **gateway** (5001→5000) and the **frontend** publicly. Do **not** expose the
Postgres (5435–5439) or individual service (8081–8086) ports in production — bind them to
localhost or drop the host mappings.

Routing rules your proxy must implement (all over HTTPS with the wildcard cert):

- `support.<domain>`  → frontend (platform portal)
- `<slug>.<domain>`   → frontend (tenant app)
- API calls (`/api/**`) → **gateway** (`lante-gateway:5000`)

**Preserve headers.** The frontend sends `Authorization` (no `X-Tenant-Subdomain` since the
2026-07 single-login change); the gateway sets CORS by reflecting `Origin`. Ensure the proxy
forwards `Origin` and `Host`, and does not strip `X-Tenant-*` headers — `X-Tenant-Schema` is
used for internal service-to-service calls.

> **Do not have the proxy inject `X-Tenant-Schema` from anything client-supplied.** Services
> honour that header only when the caller also presents a valid `X-Internal-Key`, precisely
> because anonymous callers can set arbitrary headers on their own requests. The JWT `schema`
> claim always wins. `scripts/ci/validate_tenant_interceptors.py` enforces that ordering.

---

## 8. Deploy (fresh install)

```bash
cd Deployment

# 1. Configure secrets
cp .env .env.local   # or edit .env directly; rotate every # ROTATE value
$EDITOR .env

# 2. Sanity-check interpolation (no ${VAR} should remain unresolved)
docker compose -p lante config >/dev/null && echo OK

# 3. Build + start the stack
docker compose -p lante up -d --build

# 4. Watch health
docker compose -p lante ps
```

On startup each service runs `Database.MigrateAsync()` against its own DB, and the
**user-service also runs `DatabaseSeeder`** (platform admin, permission catalog, subscription
plans). See §10 for a required action item about the seeder.

Then build/serve the frontend (`apps/lante_frontend`): set its `.env`, `npm ci && npm run build`,
and serve `dist/` behind the proxy (or `npm run dev` for local).

---

## 9. First-run verification

```bash
# Gateway alive
curl -s http://localhost:5001/health

# Platform login works (control plane)
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5001/api/v1/auth/platform/login \
  -H 'Content-Type: application/json' -d '{"email":"<platform-admin>","password":"<pw>"}'
```

1. Log into the platform portal at `support.<domain>` as the platform admin.
2. Create a company (slug, Enterprise plan, services) → it **auto-provisions** all schemas.
3. Confirm all services show **Provisioned** on the company detail page.
4. Log into the tenant at `<slug>.<domain>` as the company admin with the temp password
   (`Welcome@2026!`), complete first-login password change, and confirm all modules load.

---

## 10. Seeded accounts & ⚠ pre-prod action items

- **Platform admin** (seeded by `DatabaseSeeder`): `akinyimelante@gmail.com` / `Platform@2026!`.
  **Change the email + password immediately** for production (edit the seeder constant, or
  update the row + reset the hash post-deploy).
- **⚠ The `DatabaseSeeder` still seeds legacy demo data** (old-style roles and demo users, e.g.
  `kleinmelanie04@gmail.com`, `joshuaiska@gmail.com`) into the control plane on an empty DB.
  For a clean production deployment the seeder should be trimmed to seed **only**: the platform
  admin, the global permission catalog, and the subscription plans. Track this before go-live.
- New tenant admins receive temp password **`Welcome@2026!`** and are forced to change it on
  first login.

---

## 11. Operational notes

- **Creating a company auto-provisions** all subscribed schemas. If provisioning partially fails,
  it is non-fatal at create time and can be retried from the company detail page (“Provision Schemas”).
- **Soft delete:** the UI Delete flags `IsDeleted=true` (restorable) and does **not** drop schemas
  or free the slug/admin email. To fully remove a company (e.g. to reuse a slug) an operator must
  manually drop `tenant_<slug>` in each service DB and delete the control-plane rows.
- **Adding a service to an existing company:** add its tracker row and re-run provisioning.
- **Redis** holds sessions / 2FA state; flushing it logs everyone out but is otherwise safe.

---

## 12. Backups

> ⚠️ **This section previously listed only 5 of the 14 databases** (`5435`–`5439`), silently
> omitting finance, stores, compliance, hse, subcontracts, reporting, crm, procurement and hr.
> The Kubernetes backup job had the same defect and was additionally never deployed at all —
> see #190 / #192 / #200. Do not hand-maintain a list of databases anywhere; enumerate them.

Under compose, each service has its own Postgres container, so **every one** must be backed up.
Enumerate rather than hardcode:

```bash
# All 14 service databases, discovered rather than listed
for p in 5435 5436 5437 5438 5439 5440 5441 5442 5443 5444 5445 5446 5448 5449; do
  db=$(PGPASSWORD="$DB_PASSWORD" psql -h localhost -p "$p" -U "$DB_USER" -Atc \
        "SELECT datname FROM pg_database WHERE datname LIKE 'lante_%' AND NOT datistemplate")
  [ -n "$db" ] && PGPASSWORD="$DB_PASSWORD" pg_dump -h localhost -p "$p" -U "$DB_USER" \
      -Fc -d "$db" -f "backup_${db}.dump"
done
```

Port 5447 is unused; the range is not contiguous. Current mapping is in §1.

**A tenant's data spans every service database — restore them as a set.** A partial restore
leaves a tenant whose ledger, inventory and HR records disagree with each other.

Also back up, and store **outside** the machine being backed up:

- `lante-erp-secrets` (k8s) or `.env` (compose) — losing the **JWT secret** invalidates every
  issued token; losing **`LICENSE_PRIVATE_KEY_JWK`** invalidates every issued licence, which is
  not recoverable by regenerating it.
- Uploaded files. In compose these are bind-mounted under `Deployment/uploads/`; in Kubernetes
  they are 8 separate PVCs. `pg_dump` does **not** cover them, and database rows reference
  files that would not survive a restore — certificates, ticket attachments, driver documents.

> **Kubernetes differs.** There is one `lante-postgresql` instance, not 14 containers, so the
> loop above does not apply. The k8s approach is pgBackRest physical backup plus WAL archiving
> to offsite object storage, which captures every database and every tenant schema at once —
> tracked in #200. As of 2026-08-18 **no backups run in the Kubernetes deployment at all.**

---

## 13. Production hardening checklist

- [ ] Rotate every `# ROTATE` value in `.env` (DB, app role, JWT, Redis, internal key, SMTP)
- [ ] Generate a fresh `LICENSE_PRIVATE_KEY_JWK`
- [ ] `.env` is git-ignored and injected from a secret store
- [ ] Remove/localhost-bind Postgres (5435–5439) and service (8081–8086) host ports
- [ ] Only gateway (5001) + frontend exposed, behind HTTPS
- [ ] Wildcard DNS + wildcard TLS (DNS-01) live for `*.<domain>` and `support.<domain>`
- [ ] Change the seeded platform-admin email + password
- [ ] Trim `DatabaseSeeder` to production seed only (see §10)
- [ ] Automated backups for all Postgres volumes

# Lante ERP — Kubernetes Operations Guide

Cluster: k3s v1.34.5+k3s1, single node `vmi2743443` on `164.68.116.82`
(16 CPU, 62 Gi RAM, 485 G disk)

Shell access: `ssh joshua@164.68.116.82` (key-only; password auth is disabled).
`kubectl` works for `joshua` via `~/.kube/config`. **`sudo` requires a password**, so
anything needing root — reading `/etc/rancher/k3s/`, host-level changes — needs an
interactive session.

> **Single node, no redundancy.** There is one control-plane node and no workers. Every
> service runs at `replicaCount: 1`, all volumes use the `local-path` storage class
> (a directory on this node's disk) with reclaim policy `Delete`, so deleting a PVC
> destroys its data with no recovery — and ArgoCD runs `prune: true`. Losing this host
> loses the cluster and its data together.

---

## Namespaces

| Namespace | Purpose |
|---|---|
| `new-erp` | All Lante ERP microservices, Postgres and Redis |
| `qalitrack-prod` | Qalitrack app + Prometheus, Grafana, Loki |
| `qalitrack-dev` | Qalitrack development |
| `qalitrack-monitoring` | Qalitrack monitoring |
| `kamichain-demo` | Unrelated demo workload |
| `argocd` | GitOps controller |
| `cert-manager` | TLS certificate automation |
| `kubernetes-dashboard` | Cluster dashboard |

> **This node is shared.** Lante is one of several workloads on `164.68.116.82` —
> Qalitrack occupies three namespaces and `kamichain-demo` another. They compete for
> the same CPU, memory and disk, and they fail together. Bear that in mind when
> sizing anything or diagnosing resource pressure.

---

## Public URLs

| Service | URL |
|---|---|
| ERP Frontend | https://lante.africa |
| ERP Gateway (API) | https://kmk.support.qalibrated.co.ke |
| Swagger (per service) | https://kmk.support.qalibrated.co.ke/swagger/{service}/index.html |
| ArgoCD | https://argocd.qalibrated.co.ke |
| Grafana | https://grafana.qalibrated.co.ke |
| Prometheus | https://prometheus.qalibrated.co.ke |
| Qalitrack API | https://api.qalibrated.co.ke |

### Swagger endpoints

Routed by the gateway (`packages/LanteGateway/src/LanteGateway/yarp.json`):

```
/swagger/user/index.html
/swagger/license/index.html
/swagger/tickets/index.html
/swagger/operations/index.html
/swagger/fleet/index.html
/swagger/finance/index.html
/swagger/hse/index.html
/swagger/compliance/index.html
/swagger/subcontracts/index.html
/swagger/reporting/index.html
```

Two things to know:

- **`/swagger/projects` is a legacy alias** — it still routes to
  `cluster-lante-operation-service`, i.e. the same target as `/swagger/operations`,
  from when projects were their own service. Harmless, but prefer `/swagger/operations`.
- **`crm`, `hr`, `procurement` and `store` have no swagger route**, so their Swagger
  UIs are not reachable through the gateway even though the services are deployed.

---

## ERP Services

16 Deployments — 14 backend services, the gateway, and the frontend. All run at
`replicaCount: 1` with autoscaling disabled (see "Replicas" below for why).

| Deployment / K8s Service | Port | Domain |
|---|---|---|
| `lante-gateway` | 5000 | YARP reverse proxy — JWT validation, CORS, rate limiting, tenant-suspension checks, audit log |
| `lante-frontend-service` | 80 | React 18 + Vite SPA behind nginx |
| `lante-user-service` | 8080 | Control plane — identity, auth/2FA, users, roles, permissions, tenants, subscriptions, tenant provisioning |
| `lante-operation-service` | 8080 | Largest service — projects, assignments, lab work orders, calibration certificates, service reports, timesheets |
| `lante-ticketing-service` | 8080 | Tickets, SLAs, workflow rules, knowledge base, customer portal |
| `lante-crm-service` | 8080 | Leads, deals, opportunities, quotations, tenders, customers |
| `lante-hr-service` | 8080 | Employees, payroll, leave, attendance, appraisal, recruitment |
| `lante-finance-service` | 8080 | GL/journals, AP/AR, budgets, bank rec, fixed assets, month-end, statutory |
| `lante-procurement-service` | 8080 | Requisitions, POs, suppliers, 3-way matching |
| `lante-store-service` | 8080 | Inventory — item master, stock movements, GRN, transfers |
| `lante-fleet-service` | 8080 | Trucks, drivers, trips, expenses, materials |
| `lante-compliance-service` | 8080 | Statutory & governance — returns, resolutions, policies, licences, SOPs |
| `lante-hse-service` | 8080 | Incidents, RAMS, corrective actions, PPE, toolbox talks, inspections |
| `lante-subcontracts-service` | 8080 | Subcontractors, prequalification, awards, retentions, scorecards |
| `lante-reporting-service` | 8080 | Stateless cross-service analytics (9 of 15 reports — see `docs/reporting-module-status.md`) |
| `lante-license-service` | 8080 | Cross-tenant licence/plan catalog — deliberately **not** tenant-scoped |

### Data stores (StatefulSets)

| StatefulSet | Replicas | Port |
|---|---|---|
| `lante-postgresql` | 1/1 | 5432 |
| `lante-redis-master` | 1/1 | 6379 |
| `lante-redis-replicas` | 3/3 | 6379 |

All 14 backend service databases live in the **single** `lante-postgresql` instance,
one database per service, with a `tenant_<slug>` schema per tenant inside each.

> **No `lante-technician-service` or `lante-project-service`.** Both were listed here
> previously and neither exists. `packages/microservices/technician-service/` has zero
> git-tracked files, and project work moved into `operation-service`. Ticketing still
> has a `TechnicianServiceClient`, but it reads
> `OperationsService:BaseUrl` first and calls operations — the class name is just stale.

---

## Day-to-Day Commands

### View pods
```bash
kubectl get pods -n new-erp
kubectl get pods -n qalitrack-prod
kubectl get pods -n argocd
```

### View logs
```bash
# Tail logs for a service (replace <pod-name>)
kubectl logs -n new-erp <pod-name> --tail=50 -f

# By label (no need to know exact pod name)
kubectl logs -n new-erp -l app.kubernetes.io/name=user-service --tail=50 -f
kubectl logs -n new-erp -l app.kubernetes.io/name=operation-service --tail=50 -f
kubectl logs -n new-erp -l app.kubernetes.io/name=finance-service --tail=50 -f
kubectl logs -n new-erp -l app.kubernetes.io/name=gateway --tail=50 -f
```

Valid `app.kubernetes.io/name` values are the 16 deployments listed above, minus the
`lante-` prefix — e.g. `user-service`, `operation-service`, `ticketing-service`,
`crm-service`, `hr-service`, `finance-service`, `procurement-service`, `store-service`,
`fleet-service`, `compliance-service`, `hse-service`, `subcontracts-service`,
`reporting-service`, `license-service`, `gateway`, `frontend-service`.

### Restart a service
```bash
kubectl rollout restart deployment lante-user-service -n new-erp
kubectl rollout restart deployment lante-operation-service -n new-erp
kubectl rollout restart deployment lante-gateway -n new-erp
```

### Check rollout status
```bash
kubectl rollout status deployment lante-user-service -n new-erp
```

### Describe a pod (show events, errors)
```bash
kubectl describe pod <pod-name> -n new-erp
```

### Get recent events in namespace
```bash
kubectl get events -n new-erp --sort-by='.lastTimestamp' | tail -20
```

---

## ArgoCD — GitOps

ArgoCD watches `LANTE-AFRICA/Lante-erp` main branch, path `kubernetes/helm-charts/lante-erp-platform`.  
Every push to that branch triggers a sync.

### Check sync status
```bash
kubectl get applications -n argocd
```

### Force sync from CLI
```bash
kubectl patch application lante-erp -n argocd --type merge -p '{"operation":{"sync":{"revision":"HEAD"}}}'
kubectl patch application qalitrack  -n argocd --type merge -p '{"operation":{"sync":{"revision":"HEAD"}}}'
```

### Get ArgoCD admin password
```bash
kubectl get secret argocd-initial-admin-secret -n argocd -o jsonpath='{.data.password}' | base64 -d && echo
```

---

## Helm Charts

ERP chart path: `kubernetes/helm-charts/lante-erp-platform/`  
Service sub-charts are vendored as `.tgz` files in `charts/`.

### CRITICAL: After editing a service chart template, you must repackage it
```bash
cd kubernetes/helm-charts

# Repackage a specific service chart (example: fleet-service)
helm package fleet-service -d lante-erp-platform/charts/ --version 1.0.0

# Repackage all service charts
for svc in user-service license-service ticketing-service operation-service crm-service \
           hr-service finance-service procurement-service store-service fleet-service \
           compliance-service hse-service subcontracts-service reporting-service \
           gateway-service frontend-service; do
  helm package $svc -d lante-erp-platform/charts/ --version 1.0.0 2>/dev/null && echo "Packaged $svc"
done
```

If you only edit files in `lante-erp-platform/templates/` or `lante-erp-platform/values.yaml`, no repackaging is needed.

### Validate chart rendering (dry run)
```bash
helm template lante-erp kubernetes/helm-charts/lante-erp-platform \
  -f kubernetes/helm-charts/lante-erp-platform/values.yaml \
  --debug 2>&1 | head -60
```

---

## Replicas

All services are locked to **1 replica** by disabling HPAs. Never set `replicaCount` above 1 in values.yaml.

### Check if any HPAs exist (should be none)
```bash
kubectl get hpa -n new-erp
```

### Delete all HPAs if they reappear
```bash
kubectl delete hpa --all -n new-erp
```

---

## Database Operations

**There is ONE PostgreSQL StatefulSet, not one per service.** `lante-postgresql-0` holds a
separate database per service, and inside each database a `tenant_<slug>` schema per
tenant. Older revisions of this document described a StatefulSet per service and pod names
like `user-service-postgresql-0` — those do not exist.

This matters for backups: a cluster-level physical backup of the single instance covers
every service database and every tenant schema at once, including ones added later.

### Connect

```bash
kubectl exec -it -n new-erp lante-postgresql-0 -- psql -U postgres
```

Then `\l` to list databases, `\c <db>` to switch, `\dn` to list that database's tenant
schemas.

### Databases

| Database | Service |
|---|---|
| `lante_userservice` | user-service (also the control plane: tenants, plans, platform admins) |
| `lante_operations` | operation-service |
| `lante_ticketing` | ticketing-service |
| `lante_finance` | finance-service |
| `lante_stores` | store-service |
| `lante_fleetservice` | fleet-service |
| `lante_compliance` | compliance-service |
| `lante_hse` | hse-service |
| `lante_subcontracts` | subcontracts-service |
| `lante_reporting` | reporting-service |
| `lante_licensing` | license-service (cross-tenant catalog, not tenant-scoped) |

> **Gap:** `kubernetes/secrets/generate-secrets.sh` defines connection strings for the 11
> databases above, but **none for hr, crm or procurement**, even though all three are
> deployed and their charts reference `lante-erp-secrets`. Either they are configured
> out-of-band or something is misconfigured — this is unresolved, so do not assume the
> list above is the complete set of live data.

### Schema-per-tenant

Every service database uses `tenant_<slug>` schemas; `public` holds only control-plane and
shared tables. A request's schema is set per connection by that service's
`TenantDbConnectionInterceptor`, resolved from the JWT `schema` claim. When querying by
hand, set it explicitly or you will silently read `public`:

```sql
SET search_path TO "tenant_<slug>";
```

### WARNING: Delete a database (drops all data — irreversible)
```bash
# 1. Delete StatefulSet and PVC
kubectl delete statefulset user-service-postgresql -n new-erp
kubectl delete pvc -l app.kubernetes.io/name=user-service-postgresql -n new-erp

# 2. Let ArgoCD recreate it (trigger sync or wait for auto-sync)
# 3. Restart the service so it runs EnsureCreated again
kubectl rollout restart deployment lante-user-service -n new-erp
```

---

## Gateway — YARP Config

**Ocelot was replaced by YARP.** There is no `ocelot.json`. The gateway is
`packages/LanteGateway` using `Yarp.ReverseProxy`, and its routes live in `yarp.json`.

### ⚠️ There are two copies of `yarp.json`, and only one is deployed

| File | Deployed? |
|---|---|
| `kubernetes/helm-charts/gateway-service/yarp.json` | **Yes — this is what runs** |
| `packages/LanteGateway/src/LanteGateway/yarp.json` | **No** — used for local runs only |

The deployed config comes from ConfigMap `lante-gateway-yarp-config`, generated by
`.Files.Get "yarp.json"` in
`kubernetes/helm-charts/gateway-service/templates/yarp-configmap.yaml` and mounted at
`/app/yarp.json`.

The copy under `packages/` sits next to the gateway source and reads as authoritative.
It is not. **These two have already diverged in production** — see issue #195, where two
missing calibration-certificate routes caused live 404s. Until the duplicate is removed,
edit **both** and diff them before you ship.

Local development uses `yarp.Development.json`; docker-compose uses
`yarp.DockerCompose.json`. Neither is deployed.

### Changing gateway routes

Routes are GitOps-managed. Do **not** hand-edit the ConfigMap — ArgoCD runs
`selfHeal: true`, so a manual `kubectl create configmap` or `kubectl edit` will be
reverted, usually within minutes and without telling you.

```bash
# 1. Edit the DEPLOYED copy (and mirror it into packages/ until #195 removes the duplicate)
#    kubernetes/helm-charts/gateway-service/yarp.json

# 2. Repackage the gateway subchart — REQUIRED. Skipping this changes nothing,
#    and the change will appear to have been applied. See "CRITICAL" above.
cd kubernetes/helm-charts
helm package gateway-service -d lante-erp-platform/charts/ --version 1.0.0

# 3. Commit both the yarp.json change and the regenerated .tgz, then let ArgoCD sync.
```

The Deployment carries a `checksum/yarp-config` annotation over the file's hash, so the
gateway pod rolls automatically when the config changes — no manual restart needed.

### Inspect what is actually running

```bash
# The deployed route table
kubectl get configmap lante-gateway-yarp-config -n new-erp \
  -o jsonpath='{.data.yarp\.json}' | python3 -m json.tool | less

# Confirm a route exists: 401 = deployed and requires auth, 404 = no such route
curl -s -o /dev/null -w '%{http_code}\n' https://kmk.support.qalibrated.co.ke/api/v1/<path>
```

That 401-vs-404 check is the quickest way to tell "the route is missing" from
"my token is wrong", and it is how #195 was confirmed.

---

## TLS / Certificates

cert-manager handles TLS automatically via Let's Encrypt (issuer: `letsencrypt-prod`).

### Check certificate status
```bash
kubectl get certificates -n new-erp
kubectl get certificates -n qalitrack-prod
```

### Describe a certificate (shows renewal status)
```bash
kubectl describe certificate lante-erp-tls -n new-erp
```

### Check certificate requests (if issuance is stuck)
```bash
kubectl get certificaterequests -n new-erp
kubectl get challenges -n cert-manager
```

---

## Prometheus / Grafana / Loki

Prometheus scrapes both `new-erp` and `qalitrack-prod` namespaces.  
Loki collects logs from both namespaces.

### Check Prometheus scrape targets
```bash
# Port-forward Prometheus UI
kubectl port-forward -n qalitrack-prod svc/qalitrack-prometheus-server 9090:80
# Then open http://localhost:9090/targets
```

### Query Prometheus from CLI
```bash
kubectl exec -n qalitrack-prod \
  $(kubectl get pod -n qalitrack-prod -l app=prometheus,component=server -o name | head -1) \
  -c prometheus-server -- \
  wget -qO- 'http://localhost:9090/api/v1/query?query=up{namespace="new-erp"}'
```

### Check which ERP controller metrics exist
```bash
kubectl exec -n qalitrack-prod \
  $(kubectl get pod -n qalitrack-prod -l app=prometheus,component=server -o name | head -1) \
  -c prometheus-server -- \
  wget -qO- 'http://localhost:9090/api/v1/label/controller/values'
```

> Note: ERP controller metrics only appear after real API traffic hits those controllers.
> Health-check and /metrics scrapes do NOT generate `controller` labels.

### Grafana dashboards
- **ASP.NET Core controller summary** — shows per-controller request rates for both ERP and Qalitrack
- Access: https://grafana.qalibrated.co.ke

---

## Git Push — Both Repos Required

Every ERP commit must be pushed to **both** repositories:

```bash
git push upstream main   # LANTE-AFRICA/Lante-erp  (ArgoCD source)
git push lante-fork main # Joshuaisikah/Lante-erp  (personal fork)
```

### If push is rejected (non-fast-forward)
```bash
git fetch upstream main
git rebase upstream/main
git push upstream main

git fetch lante-fork main
git rebase lante-fork/main
git push lante-fork main
```

### Check remote URLs
```bash
git remote -v
```

Expected:
```
lante-fork  https://github.com/Joshuaisikah/Lante-erp.git
upstream    https://github.com/LANTE-AFRICA/Lante-erp.git
```

---

## Kubernetes Dashboard

Access: https://dashboard.qalibrated.co.ke  
Authentication: long-lived token (not skip-login — skip-login breaks on k3s).

### Get dashboard token
```bash
kubectl get secret -n kubernetes-dashboard \
  $(kubectl get serviceaccount admin-user -n kubernetes-dashboard -o jsonpath='{.secrets[0].name}' 2>/dev/null || \
    kubectl get secret -n kubernetes-dashboard -o name | grep admin-user | head -1 | cut -d/ -f2) \
  -o jsonpath='{.data.token}' | base64 -d && echo
```

---

## Common Troubleshooting

### Pod stuck in CrashLoopBackOff
```bash
# Check logs
kubectl logs -n new-erp <pod-name> --previous

# Check events
kubectl describe pod <pod-name> -n new-erp
```

### ArgoCD shows Degraded / OutOfSync
```bash
# Check what ArgoCD sees
kubectl get application lante-erp -n argocd -o yaml | grep -A20 conditions

# Force sync
kubectl patch application lante-erp -n argocd --type merge \
  -p '{"operation":{"sync":{"revision":"HEAD"}}}'
```

### Service returns 404 at gateway
A 404 from the gateway almost always means **no route matched**, not that the downstream
service is down — a healthy service behind a missing route still 404s, and a protected
route with a bad token returns **401**, not 404. Use that to tell them apart.

1. Check whether the route exists in the **deployed** config — not the copy under
   `packages/`, which is not what runs (see "Gateway — YARP Config"):
   ```bash
   kubectl get configmap lante-gateway-yarp-config -n new-erp \
     -o jsonpath='{.data.yarp\.json}' | grep -A3 '<your-path>'
   ```
2. Confirm from outside: `401` = route deployed, `404` = route absent.
   ```bash
   curl -s -o /dev/null -w '%{http_code}\n' https://kmk.support.qalibrated.co.ke/api/v1/<path>
   ```
3. If the route is in `packages/.../yarp.json` but not in the ConfigMap, you have hit the
   duplicate-file divergence in #195. Fix the chart copy and repackage.
4. Only then check gateway logs and downstream health:
   ```bash
   kubectl logs -n new-erp -l app.kubernetes.io/name=gateway --tail=50
   kubectl get pods -n new-erp
   ```

### Image not updated after CI build
ArgoCD syncs on Git changes, not on new image tags (if using `latest`).  
Force a rollout to pull the new image:
```bash
kubectl rollout restart deployment lante-gateway -n new-erp
kubectl rollout restart deployment lante-user-service -n new-erp
# ... repeat for affected service
```

### HTTP instead of HTTPS
The `redirect-https` Traefik middleware forces HTTP→HTTPS on all ERP ingress routes.  
If it breaks, check:
```bash
kubectl get middleware -n new-erp
kubectl describe middleware redirect-https -n new-erp
```

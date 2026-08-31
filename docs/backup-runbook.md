# Backup & Disaster Recovery Runbook

**Status:** Built (GitHub issue #200, PRs 1–5), not yet enabled in the live cluster.
**Scope:** what's automated, what a human still has to do, and how to alert on it going stale.

## What's automated (once enabled)

| Job | Schedule | Covers | Retention |
|---|---|---|---|
| `lante-backup-pgbackrest` | Daily 01:00 (full on Sunday, differential other days) + continuous WAL push | All 14 Postgres databases, physical/PITR-capable | 4 full backups |
| `lante-backup-logical-dumps` | Daily 02:00 | `pg_dump` of every `lante_*` database, discovered live via `pg_database` — never a hardcoded list | 5 years (Tax Procedures Act 2015 §23(1)(c) floor) |
| `lante-backup-k3s-sqlite` | Daily 03:00 | k3s control-plane state (`/var/lib/rancher/k3s/server/db/state.db`, via SQLite's online-backup API — this node runs the default SQLite datastore, not etcd) | Same PVC as logical dumps |
| `lante-backup-secrets-snapshot` | Daily 05:00 | GPG-encrypted export of the live `lante-erp-secrets` Secret. **Export only** — never regenerates or rotates anything | Same PVC as logical dumps |
| `lante-backup-verify` | Weekly, Sunday 04:00 | Restores the latest logical dump into a scratch Postgres and re-runs the same catalog queries captured at dump time; hard-fails on any mismatch | n/a |
| Velero (`lante-backup-velero` ArgoCD Application) | Daily 02:30 | The 8 upload-owning PVCs (certificates, ticket attachments, driver documents — opted in via the `backup.velero.io/backup-volumes: uploads` pod annotation, not swept in bulk) | 30 days (`ttl: 720h`) |

All five CronJobs live in `kubernetes/helm-charts/lante-erp-platform/templates/backup-*.yaml`, gated by `backups.enabled` (default `false`) and finer-grained sub-flags. Every secret reference (`lante-backup-minio-credentials`, `lante-backup-b2-credentials`) is `optional: true` on the consuming pod — a missing secret degrades to "job soft-skips with a log line," never a crash loop or a failed sync.

## What a human still has to do

1. **Provision real offsite storage.** Nothing above has a real offsite destination yet. The interim target is a self-hosted MinIO instance in-cluster (`kubernetes/velero/`, `kubernetes/argocd/minio-application.yaml`) — durable against pod/PVC loss but **not** against node loss, since it's the same physical box. Backblaze B2 was the original choice (see #200) but no account/bucket/key exists. `lante-backup-b2-credentials` now holds `BACKUP_ENCRYPTION_PASSPHRASE` (#409 — live-verified: a real run produces a decryptable snapshot), but **not** `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY` — those still need a real B2 account before `lante-backup-logical-dumps` starts actually shipping dumps offsite instead of keeping them local-only. Until then, treat this as "secrets snapshots are for real now; dumps are backed up against most failure modes, not against total node loss."
2. **Bootstrap the two backup-related ArgoCD Applications, once:**
   ```
   kubectl apply -f kubernetes/argocd/minio-application.yaml
   kubectl apply -f kubernetes/argocd/velero-application.yaml
   ```
   These are outside the parent chart ArgoCD already watches, by design — same bootstrap pattern as `lante-erp-application.yaml` itself.
3. **Create `lante-backup-minio-credentials`** — done automatically by the MinIO chart on first sync (`kubernetes/velero/templates/minio-secret.yaml`, generated once via `lookup`, stable across re-syncs). No manual step needed here, but confirm it exists before trusting the pgBackRest/Velero jobs: `kubectl get secret lante-backup-minio-credentials -n new-erp`.
4. **Flip `backups.enabled: true`** in `values.yaml` (plus `backups.k3sSqlite.enabled: true` separately — it needs node-pinning and a hostPath mount, more cluster-topology-specific than the others, so it's opt-in on its own).
5. **Restart Postgres to pick up `archive_mode = on`.** This is a Postgres server parameter, not reloadable — plan the restart window, don't just let ArgoCD sync it in. Until the restart happens, WAL simply accumulates in `pg_wal` (present but not archived) — not a failure state, but watch disk usage if the restart is delayed by more than a day or two.
6. **Verify once by hand before trusting the schedule:**
   ```
   kubectl exec lante-postgresql-0 -n new-erp -c postgresql -- pgbackrest --stanza=lante info
   kubectl create job --from=cronjob/lante-backup-logical-dumps  manual-test-dump   -n new-erp
   kubectl create job --from=cronjob/lante-backup-verify         manual-test-verify -n new-erp
   ```
7. **Rehearse a full destructive restore once**, after step 6 has been green for a few days. This is pre-launch / mostly test data right now — this is the cheapest this rehearsal will ever be. Write down the actual wall-clock time; that number is the real RTO, not an estimate.

## Freshness alerting (manual Grafana step — not in either repo's GitOps state)

`qalitrack-kube-state-metrics` already watches CronJobs/Jobs cluster-wide, including `new-erp` — the moment the CronJobs above exist, `kube_cronjob_status_last_schedule_time` and `kube_job_status_failed` are queryable for free against the existing QaliTrack Prometheus datasource. No ServiceMonitor or scrape-config change is needed for these two rules specifically.

Add two alert rules in Grafana (Alerting → Alert rules → New alert rule), datasource = the existing QaliTrack Prometheus:

**1. Missed schedule** — fires if any backup CronJob hasn't run in over 36h (covers the daily jobs with margin; the weekly verify job needs its own longer threshold):
```promql
# Daily jobs (pgbackrest, logical-dumps, k3s-sqlite, secrets-snapshot)
time() - max by (cronjob) (
  kube_cronjob_status_last_schedule_time{namespace="new-erp", cronjob=~"lante-backup-(pgbackrest|logical-dumps|k3s-sqlite|secrets-snapshot)"}
) > 36 * 3600
```
```promql
# Weekly job (verify) — 8-day threshold
time() - max by (cronjob) (
  kube_cronjob_status_last_schedule_time{namespace="new-erp", cronjob="lante-backup-verify"}
) > 8 * 24 * 3600
```

**2. Job failure** — fires on any failed run of any backup job:
```promql
max by (job_name) (
  kube_job_status_failed{namespace="new-erp", job_name=~"lante-backup-.*"}
) > 0
```

Route both to whatever channel already gets QaliTrack's platform alerts — do not create a new notification channel just for this.

**Explicitly out of scope, flagged as a cross-team gap:** `qalitrack-prometheus-pushgateway` exists and nothing currently scrapes it. Richer custom metrics (per-database dump size, restore-verification pass/fail as a first-class metric rather than just Job exit code, etc.) could be pushed there for higher-fidelity alerting, but that requires a human with access to the *separate* QaliTrack platform chart to add a scrape job. Don't treat this doc as having closed that gap — it hasn't. The two rules above work today without it.

## Explicitly not covered by Tier-0

- Row-for-row diffing of restored data against live production (the verify job checks internal consistency of the backup, not drift from a point-in-time production snapshot).
- A second copy of the offsite storage itself (MinIO today, B2 later — both are a single logical destination).
- Full S3 migration of the 8 upload PVCs off local disk entirely — Velero backs them up in place; moving the primary storage itself is a separate, larger project.

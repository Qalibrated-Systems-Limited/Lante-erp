#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# Creates the least-privilege runtime role `qalicore_app` on first DB init.
#
# Business services connect at RUNTIME as this role (ConnectionStrings__AppConnection),
# while migrations/provisioning run as the superuser (DefaultConnection). Per-tenant
# schema grants are applied automatically during provisioning (GrantAppRoleAsync);
# this script only creates the role + baseline CONNECT/USAGE.
#
# Runs only when the data volume is empty (Postgres docker-entrypoint-initdb.d contract).
# Password is read from the QALICORE_APP_PASSWORD env var passed by docker-compose.
# ─────────────────────────────────────────────────────────────────────────────
set -e

: "${QALICORE_APP_PASSWORD:?QALICORE_APP_PASSWORD must be set}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'qalicore_app') THEN
            CREATE ROLE qalicore_app LOGIN PASSWORD '${QALICORE_APP_PASSWORD}';
        END IF;
    END
    \$\$;

    GRANT CONNECT ON DATABASE "$POSTGRES_DB" TO qalicore_app;
    GRANT USAGE ON SCHEMA public TO qalicore_app;
EOSQL

echo "qalicore_app role ensured on database $POSTGRES_DB"

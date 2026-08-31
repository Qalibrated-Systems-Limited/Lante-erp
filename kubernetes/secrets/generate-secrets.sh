#!/bin/bash
# Lante ERP - Generate Kubernetes Secrets
# Run this once to create your secrets file, then run create-k8s-secrets.sh
#
# All 14 active services share a single Postgres instance (lante-postgresql) —
# one database per service, no schema/table merging. DB_USER/DB_PASSWORD is the
# shared migration superuser; APP_DB_USER/APP_DB_PASSWORD (qalicore_app) is the
# least-privilege runtime role business services connect as. See
# kubernetes/helm-charts/lante-erp-platform values.yaml
# (postgresql.primary.initdb.scripts) for how both are provisioned — that
# script's DATABASES= list is the source of truth; keep this file's connection
# strings in sync with it (see scripts/ci/validate_database_list_consistency.py,
# which checks exactly this).

set -e

echo "Generating Lante ERP secrets..."

# Generate strong random secrets
#
# JWT (#215): user-service alone signs tokens now, with an RSA keypair instead of the old
# shared HS256 secret every service could both issue and verify with. JWT_SIGNING_KEYS is a JSON
# array — [{kid, privateKeyPem, active}] — so rotation is adding a new entry and flipping "active",
# not a redeploy of all 16 services: every listed key's PUBLIC half is published via user-service's
# JWKS endpoint regardless of which one is "active", so a token signed by a just-retired key still
# validates until that entry is actually removed.
JWT_KID="jwt-$(date -u +%Y%m%d%H%M%S)"
JWT_RSA_PRIVATE_KEY_PEM=$(openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 2>/dev/null)
JWT_SIGNING_KEYS=$(python3 -c "
import json, sys
print(json.dumps([{'kid': sys.argv[1], 'privateKeyPem': sys.argv[2], 'active': True}]))
" "$JWT_KID" "$JWT_RSA_PRIVATE_KEY_PEM")
INTERNAL_SERVICE_KEY=$(openssl rand -base64 32)
DB_PASSWORD=$(openssl rand -base64 24)
APP_DB_PASSWORD=$(openssl rand -base64 24)
REDIS_PASSWORD=$(openssl rand -base64 24)
TENANT_SECRETS_ENCRYPTION_KEY=$(openssl rand -base64 32)

DB_USER="postgres"
APP_DB_USER="qalicore_app"
DB_HOST="lante-postgresql"

# Build connection strings — one database per service on the shared host
USER_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_userservice;Username=${DB_USER};Password=${DB_PASSWORD}"
LICENSE_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_licensing;Username=${DB_USER};Password=${DB_PASSWORD}"
TICKETING_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_ticketing;Username=${DB_USER};Password=${DB_PASSWORD}"
FLEET_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_fleetservice;Username=${DB_USER};Password=${DB_PASSWORD}"
OPERATION_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_operations;Username=${DB_USER};Password=${DB_PASSWORD}"
FINANCE_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_finance;Username=${DB_USER};Password=${DB_PASSWORD}"
STORE_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_stores;Username=${DB_USER};Password=${DB_PASSWORD}"
HSE_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_hse;Username=${DB_USER};Password=${DB_PASSWORD}"
COMPLIANCE_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_compliance;Username=${DB_USER};Password=${DB_PASSWORD}"
SUBCONTRACTS_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_subcontracts;Username=${DB_USER};Password=${DB_PASSWORD}"
REPORTING_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_reporting;Username=${DB_USER};Password=${DB_PASSWORD}"
CRM_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_crm;Username=${DB_USER};Password=${DB_PASSWORD}"
HR_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_hr;Username=${DB_USER};Password=${DB_PASSWORD}"
PROCUREMENT_DB_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_procurement;Username=${DB_USER};Password=${DB_PASSWORD}"

# App-role (qalicore_app) connection strings — used by business services at runtime
USER_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_userservice;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
LICENSE_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_licensing;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
TICKETING_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_ticketing;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
FLEET_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_fleetservice;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
OPERATION_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_operations;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
STORE_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_stores;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
HSE_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_hse;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
COMPLIANCE_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_compliance;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
SUBCONTRACTS_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_subcontracts;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
REPORTING_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_reporting;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
FINANCE_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_finance;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
CRM_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_crm;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
HR_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_hr;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"
PROCUREMENT_APP_CONNECTION="Host=${DB_HOST};Port=5432;Database=lante_procurement;Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"

REDIS_CONNECTION="lante-redis-master:6379,password=${REDIS_PASSWORD}"

cat > secrets.env << ENVEOF
JWT_SIGNING_KEYS=${JWT_SIGNING_KEYS}
INTERNAL_SERVICE_KEY=${INTERNAL_SERVICE_KEY}
DB_USER=${DB_USER}
DB_PASSWORD=${DB_PASSWORD}
APP_DB_USER=${APP_DB_USER}
APP_DB_PASSWORD=${APP_DB_PASSWORD}
REDIS_PASSWORD=${REDIS_PASSWORD}
TENANT_SECRETS_ENCRYPTION_KEY=${TENANT_SECRETS_ENCRYPTION_KEY}
USER_DB_CONNECTION=${USER_DB_CONNECTION}
LICENSE_DB_CONNECTION=${LICENSE_DB_CONNECTION}
TICKETING_DB_CONNECTION=${TICKETING_DB_CONNECTION}
FLEET_DB_CONNECTION=${FLEET_DB_CONNECTION}
OPERATION_DB_CONNECTION=${OPERATION_DB_CONNECTION}
FINANCE_DB_CONNECTION=${FINANCE_DB_CONNECTION}
STORE_DB_CONNECTION=${STORE_DB_CONNECTION}
HSE_DB_CONNECTION=${HSE_DB_CONNECTION}
COMPLIANCE_DB_CONNECTION=${COMPLIANCE_DB_CONNECTION}
SUBCONTRACTS_DB_CONNECTION=${SUBCONTRACTS_DB_CONNECTION}
REPORTING_DB_CONNECTION=${REPORTING_DB_CONNECTION}
CRM_DB_CONNECTION=${CRM_DB_CONNECTION}
HR_DB_CONNECTION=${HR_DB_CONNECTION}
PROCUREMENT_DB_CONNECTION=${PROCUREMENT_DB_CONNECTION}
USER_APP_CONNECTION=${USER_APP_CONNECTION}
LICENSE_APP_CONNECTION=${LICENSE_APP_CONNECTION}
TICKETING_APP_CONNECTION=${TICKETING_APP_CONNECTION}
FLEET_APP_CONNECTION=${FLEET_APP_CONNECTION}
OPERATION_APP_CONNECTION=${OPERATION_APP_CONNECTION}
STORE_APP_CONNECTION=${STORE_APP_CONNECTION}
HSE_APP_CONNECTION=${HSE_APP_CONNECTION}
COMPLIANCE_APP_CONNECTION=${COMPLIANCE_APP_CONNECTION}
SUBCONTRACTS_APP_CONNECTION=${SUBCONTRACTS_APP_CONNECTION}
REPORTING_APP_CONNECTION=${REPORTING_APP_CONNECTION}
FINANCE_APP_CONNECTION=${FINANCE_APP_CONNECTION}
CRM_APP_CONNECTION=${CRM_APP_CONNECTION}
HR_APP_CONNECTION=${HR_APP_CONNECTION}
PROCUREMENT_APP_CONNECTION=${PROCUREMENT_APP_CONNECTION}
REDIS_CONNECTION=${REDIS_CONNECTION}
# Email settings — fill these in manually
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=your-email@gmail.com
SMTP_PASSWORD=your-app-password
# License EC private key JWK — fill this in manually (keep the single quotes!)
LICENSE_PRIVATE_KEY_JWK='{"kty":"EC","crv":"P-256","x":"...","y":"...","d":"..."}'
# Tiara SMS provider — fill these in manually
TIARA_API_KEY=your-tiara-api-key
TIARA_SENDER_ID=your-tiara-sender-id
ENVEOF

echo "secrets.env generated. Edit SMTP and LICENSE_PRIVATE_KEY_JWK, then run create-k8s-secrets.sh"
echo "IMPORTANT: Never commit secrets.env to git!"

#!/bin/bash
# Lante ERP - Create Kubernetes Secret from secrets.env
#
# Break-glass / first-time bootstrap only. Once kubernetes/secrets/sealed/ has a sealed manifest
# (see seal-secrets.sh and #222), rotations should go through that instead — this script applies
# plaintext directly to the live cluster and leaves no record in git of what changed or when.
#
# Run: bash create-k8s-secrets.sh

set -e

NAMESPACE="new-erp"
SECRET_NAME="lante-erp-secrets"

if [ ! -f secrets.env ]; then
  echo "secrets.env not found. Run generate-secrets.sh first."
  exit 1
fi

# Reconstructs each value from everything after the FIRST '=' on its line, and every key present
# in the file rather than a hand-enumerated list — sourcing secrets.env directly would let bash
# treat ';' inside connection strings (Host=x;Port=5432;...) as statement separators, and a
# hand-enumerated list silently drifts as keys get added to generate-secrets.sh (this one already
# had: three services' DB connections, TENANT_SECRETS_ENCRYPTION_KEY, and the Tiara SMS keys all
# missing before this fix — re-running this script would have silently dropped them from the live
# Secret).
FROM_LITERAL_ARGS=()
while IFS= read -r line; do
  case "$line" in
    ''|'#'*) continue ;;
  esac
  key="${line%%=*}"
  value="${line#*=}"
  case "$value" in \"*|\'*) value="${value:1}" ;; esac
  case "$value" in *\"|*\') value="${value%?}" ;; esac
  FROM_LITERAL_ARGS+=(--from-literal="${key}=${value}")
done < secrets.env

echo "Creating namespace if not exists..."
kubectl apply -f ../namespaces/new-erp.yaml

echo "Creating GHCR pull secret..."
kubectl create secret docker-registry ghcr-credentials \
  --docker-server=ghcr.io \
  --docker-username="${GHCR_USERNAME}" \
  --docker-password="${GHCR_TOKEN}" \
  --namespace="${NAMESPACE}" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "Creating lante-erp-secrets (${#FROM_LITERAL_ARGS[@]} keys)..."
kubectl create secret generic "${SECRET_NAME}" \
  --namespace="${NAMESPACE}" \
  "${FROM_LITERAL_ARGS[@]}" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "Secrets created successfully in namespace: ${NAMESPACE}"

#!/bin/bash
# Lante ERP - Seal secrets.env into a committable SealedSecret (#222)
#
# Replaces the "run create-k8s-secrets.sh against a live cluster by hand" step with one that
# produces an encrypted manifest safe to commit: kubernetes/secrets/sealed/lante-erp-secrets.sealed.yaml.
# ArgoCD (lante-secrets-application.yaml) applies it automatically from there — a rotation becomes
# "edit secrets.env, run this, commit", not an out-of-band kubectl command someone has to remember
# to run and nobody else can reproduce.
#
# Requires: the kubeseal CLI (https://github.com/bitnami/sealed-secrets#kubeseal), and either
# cluster access to fetch the lante-sealed-secrets controller's public cert live (default), or a
# previously-saved cert via --cert-file for offline sealing (see ROTATION.md).
#
# Run: bash seal-secrets.sh [--cert-file path/to/pub-cert.pem]

set -e

NAMESPACE="new-erp"
SECRET_NAME="lante-erp-secrets"
CONTROLLER_NAME="lante-sealed-secrets"
OUT_FILE="sealed/lante-erp-secrets.sealed.yaml"
CERT_FILE=""

while [ $# -gt 0 ]; do
  case "$1" in
    --cert-file) CERT_FILE="$2"; shift 2 ;;
    *) echo "Unknown argument: $1"; exit 1 ;;
  esac
done

if ! command -v kubeseal >/dev/null 2>&1; then
  echo "kubeseal not found. Install it: https://github.com/bitnami/sealed-secrets#kubeseal"
  exit 1
fi

if [ ! -f secrets.env ]; then
  echo "secrets.env not found. Run generate-secrets.sh first (or edit an existing one to rotate a value)."
  exit 1
fi

mkdir -p sealed

# Reconstructs each value from everything after the FIRST '=' on its line — sourcing secrets.env
# directly would let bash treat ';' inside connection strings (Host=x;Port=5432;...) as statement
# separators, and a naive split on every '=' would truncate JWT_SIGNING_KEYS/base64 values that
# contain '=' padding. Same approach create-k8s-secrets.sh uses, applied to every key generically
# instead of a hand-enumerated list — that list has drifted out of sync with secrets.env before
# (three services' DB connections were silently missing from it) and a generic loop can't drift.
FROM_LITERAL_ARGS=()
while IFS= read -r line; do
  case "$line" in
    ''|'#'*) continue ;;
  esac
  key="${line%%=*}"
  value="${line#*=}"
  # Strip one leading and one trailing quote char if present (either ' or "), same as
  # create-k8s-secrets.sh's get_val — LICENSE_PRIVATE_KEY_JWK is wrapped in literal single quotes
  # in secrets.env so its embedded commas/braces don't confuse anything reading the file.
  case "$value" in \"*|\'*) value="${value:1}" ;; esac
  case "$value" in *\"|*\') value="${value%?}" ;; esac
  FROM_LITERAL_ARGS+=(--from-literal="${key}=${value}")
done < secrets.env

echo "Sealing ${#FROM_LITERAL_ARGS[@]} keys from secrets.env..."

CERT_ARGS=(--controller-name="${CONTROLLER_NAME}" --controller-namespace="${NAMESPACE}")
if [ -n "${CERT_FILE}" ]; then
  CERT_ARGS=(--cert="${CERT_FILE}")
fi

kubectl create secret generic "${SECRET_NAME}" \
  --namespace="${NAMESPACE}" \
  "${FROM_LITERAL_ARGS[@]}" \
  --dry-run=client -o yaml \
  | kubeseal --format=yaml "${CERT_ARGS[@]}" \
  > "${OUT_FILE}"

echo "Wrote ${OUT_FILE}. Commit it — the content is ciphertext, safe to push."
echo "ArgoCD (lante-secrets-application.yaml) applies it automatically once merged to main."

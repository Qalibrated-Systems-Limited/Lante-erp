#!/bin/bash
# Install Kubernetes Dashboard and configure ingress at dashboard.qalibrated.co.ke

set -e

echo "Installing Kubernetes Dashboard v3.0.0..."
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml

echo "Waiting for dashboard pods to be ready..."
kubectl wait --namespace kubernetes-dashboard \
  --for=condition=ready pod \
  --selector=k8s-app=kubernetes-dashboard \
  --timeout=120s

echo "Applying service account, RBAC, and ingress..."
kubectl apply -f kubernetes-dashboard.yaml

echo "Patching dashboard for k3s (token-ttl=0 keeps the long-lived SA token valid)..."
kubectl patch deployment kubernetes-dashboard -n kubernetes-dashboard --type='json' -p='[
  {"op": "replace", "path": "/spec/template/spec/containers/0/args", "value": [
    "--auto-generate-certificates",
    "--namespace=kubernetes-dashboard",
    "--token-ttl=0"
  ]}
]'

kubectl rollout status deployment/kubernetes-dashboard -n kubernetes-dashboard --timeout=120s

echo ""
echo "Dashboard available at: https://dashboard.qalibrated.co.ke"
echo "Browser will prompt for username/password (BasicAuth via Traefik)."
echo "Then paste the long-lived token on the dashboard login page:"
echo ""
kubectl get secret admin-user-token -n kubernetes-dashboard -o jsonpath='{.data.token}' | base64 -d
echo ""

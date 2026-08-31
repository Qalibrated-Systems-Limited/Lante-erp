# Secret rotation (#222)

Prerequisite, once per cluster: `lante-sealed-secrets` (the controller) and `lante-secrets` (the
synced manifest) Applications are both registered in ArgoCD — see `sealed/README.md`. Everything
below assumes that's already done.

## Standard rotation (most keys)

Most keys in `lante-erp-secrets` — DB passwords, `INTERNAL_SERVICE_KEY`, `REDIS_PASSWORD`, SMTP,
Tiara — are safe to regenerate and redeploy independently:

1. Edit the value in `secrets.env` (or regenerate it, e.g. `openssl rand -base64 24`).
2. `./seal-secrets.sh`, commit `sealed/lante-erp-secrets.sealed.yaml`.
3. ArgoCD applies the new ciphertext; the controller decrypts it into the live `lante-erp-secrets`
   Secret. Pods do **not** pick up a changed Secret automatically when it's mounted as an env var
   (as everything here is) — roll the affected Deployments:
   `kubectl -n new-erp rollout restart deployment -l app.kubernetes.io/part-of=lante-erp`
   (or a narrower `-l` selector / explicit Deployment name if only one credential changed, e.g. a
   DB password only needs the services that actually connect to that database restarted).

A DB password rotation additionally needs the Postgres role's password changed to match *before*
step 3's restart — this script only reseals the Secret, it doesn't touch Postgres.

## `JWT_SIGNING_KEYS` — additive, not a swap

Since #215, `JWT_SIGNING_KEYS` is a JSON array of `{kid, privateKeyPem, active}`, and user-service
publishes every listed key's *public* half via its JWKS endpoint regardless of `active` — that's
what makes rotation possible without a synchronized-restart outage:

1. Generate a new RSA keypair, append a new `{kid, privateKeyPem, active: true}` entry, and set
   every other entry's `active` to `false` — **do not remove them yet**. Reseal and deploy as above.
2. user-service now signs new tokens with the new key. Old tokens still validate (their `kid`'s
   public key is still published) until they expire naturally — `JwtSettings:ExpiryInMinutes`
   (480 = 8h) is the longest anyone can be validly using a retired key.
3. After that window has fully passed, remove the retired entry from the array entirely, reseal,
   and deploy again. Only now is the old private key actually gone from the live Secret.

## Keys that must never be casually regenerated

- **`LICENSE_PRIVATE_KEY_JWK`** — signs every licence this platform has ever issued. Regenerating
  it invalidates all of them simultaneously. There is no rotation procedure for this one without a
  broader licence-reissuance plan; treat any change to it as a product/business decision, not a
  routine ops task.
- **`TENANT_SECRETS_ENCRYPTION_KEY`** — AES key used to encrypt tenant email credentials at rest
  (see `AesEmailCredentialProtector.cs` in user-service). Rotating it makes every already-encrypted
  credential undecryptable unless each one is re-encrypted with the new key first — this needs a
  migration, not a reseal-and-restart.
- The old shared symmetric `JWT_SECRET_KEY`/`JwtSettings:SecretKey` config keys are dead since
  #215's asymmetric cutover — nothing in the deployed Helm charts injects them anymore. If you find
  a service still reading one, that's a regression in #215, not a rotation question.

## What this doesn't cover

- `ghcr-credentials` (the GHCR image pull secret) is still created directly by
  `create-k8s-secrets.sh` from `GHCR_USERNAME`/`GHCR_TOKEN` env vars, not sealed — it's a
  short-lived CI-scoped token rather than long-lived application secret material, and out of
  scope for this pass.
- Comparing External Secrets Operator and SOPS against Sealed Secrets, per #222's original ask:
  Sealed Secrets was chosen as the smallest step for a single cluster — no external secret store
  to run or pay for, and the ciphertext lives in this same repo next to everything it configures.
  ESO would make more sense if a real secret manager (Vault, AWS Secrets Manager, etc.) enters the
  picture later; SOPS is a reasonable alternative but Sealed Secrets' "only the cluster can
  decrypt, full stop" model needs no separate key-management story of its own.

# Sealed secrets (#222)

This directory is what `lante-secrets-application.yaml` syncs — ArgoCD applies everything here
automatically (see that file for why `automated` is safe for ciphertext specifically).

It starts empty. To populate it for the first time:

1. Apply `kubernetes/argocd/sealed-secrets-application.yaml` and wait for the `lante-sealed-secrets`
   Application to report `Synced`/`Healthy` — the controller must be running before anything here
   can ever be decrypted.
2. Apply `kubernetes/argocd/lante-secrets-application.yaml` (safe to do in either order relative
   to step 1 — it'll just show nothing to sync until step 3).
3. From `kubernetes/secrets/`, with an existing `secrets.env` (see `generate-secrets.sh`), run
   `./seal-secrets.sh`. It writes `lante-erp-secrets.sealed.yaml` here.
4. Commit the generated file. ArgoCD picks it up from here on — no more manual `kubectl apply` of
   plaintext secrets.

See `../ROTATION.md` for how to rotate individual values afterwards, and why two of them
(`LICENSE_PRIVATE_KEY_JWK`, and `JWT_SIGNING_KEYS`' existing entries) need more care than "just
regenerate and reseal".

Do not hand-edit or commit anything in this directory except `seal-secrets.sh`'s own output — a
SealedSecret's ciphertext is meaningless without the exact name/namespace it was sealed for
(kubeseal's default `strict` scope), so anything here should always be a fresh script run, never a
hand patch.

# Exact missing owner decision

I approve one Diten.Web integration owner to apply `COMBINED-SHARED-UI-CANDIDATE.patch` SHA-256 `1e53a3a63661e85b6a165dcf8aaf48451cc077af85713234e22496ec6e861c45` only when every baseline and target hash in `BASELINE-PATCH-TARGET.tsv` SHA-256 `0a8b1e48d53630070bacbce61294bc8ecf5d82961ab7196dea0794dd81c47470` matches.

The approved behavior is limited to: (1) preserving ordinary HTML cookie-auth login redirects while the five metadata-marked Shipment same-origin JSON adapters return the existing v1 `401 INVALID_REQUEST` JSON/correlation surface when unauthenticated; and (2) sourcing DataTables responsive modal `Details` and `Close` chrome from the seven-language shared localization payload. Controller-wide `[Authorize]`, authenticated `403`, global login routing and auth security remain unchanged.

The integration owner must apply the combined patch once, run its focused regressions, and hand the immutable final source to a different verifier for pipeline and 390/768 seven-culture browser checks. This decision does not authorize Shipment accessibility changes, global layout changes, Gateway/Auth/backend changes, canonical/guard/pack changes, rollout, commit, push or full-module acceptance.

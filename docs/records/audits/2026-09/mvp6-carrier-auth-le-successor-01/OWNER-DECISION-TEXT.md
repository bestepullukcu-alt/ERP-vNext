# UNAPPROVED — exact successor implementation decision

I approve the single `MVP6-CARRIER-AUTH-LE-SUCCESSOR-01` candidate for isolated implementation and independent verification.

Exact artifacts:

- successor patch SHA-256: `e6b2a3d335ee8f76270fe6ace09793a08d69e57174874be2b2db99941b01f673`
- source manifest SHA-256: `275f204c29113b2bb80b9aa10c87e08b81bd2ab2c71c59a92a98d18130ccb93d`
- source archive SHA-256: `1c497022407788a5b38f5c554c867d446cc08541cf00c0d3390704c466301846`

**Platform/Auth scope:** I approve the endpoint-local tenant/actor context binding and the narrow Platform-to-MDM service-token production contained in the exact manifest. The existing global `/api/internal` behavior, client LE non-authority and exactly-one claim policy remain unchanged.

**MDM/security scope:** I approve the exact internal LE reference-validation endpoint and service-token validator contained in the manifest. It may authorize only caller `Diten.Platform`, audience `diten-mdm-reference-validation`, scope `mdm.legal-entities.reference.validate`, and the exact signed tenant/actor/LE tuple. Headers alone are not authority. Disablement, key rotation, revocation/configuration failure, mismatch, expiry and dependency failure remain fail-closed.

The implementation lane may configure a matching ephemeral secret without storing it in evidence and may execute login, refresh, MFA, forced-password, zero/multiple/inactive/revoked and dependency-failure scenarios in isolated environments. A different agent must independently verify the result.

This decision does not authorize global tenant bypass expansion, general MDM read access, MDM auth removal, Carrier 403 relaxation, Supplier concurrence, gateway/permission/contract changes, operational rollout, commit, push or stash. Any patch, manifest, caller, audience, scope or path change requires a new decision.

Status: **UNAPPROVED until the owner sends this exact decision or an equivalent hash-bound message.**

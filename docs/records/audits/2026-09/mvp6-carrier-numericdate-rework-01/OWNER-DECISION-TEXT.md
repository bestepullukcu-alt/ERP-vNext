# UNAPPROVED — NumericDate JSON-type rework decision

I approve the `MVP6-CARRIER-NUMERICDATE-REWORK-01` candidate for isolated application and independent verification.

Exact artifacts:

- candidate patch SHA-256: `180143bccc08ad3316eb89f79a3e9386da2ce881b9a4b02f00a725b0be27ef4b`;
- two-file delta manifest SHA-256: `aed4ddd904ee78120ca47c23b0a65b328c7ae88771bda62183f4f72777b6f172`;
- successor final 22-path manifest SHA-256: `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`;
- successor build-source manifest SHA-256: `516ba828bed602e4355a5cc6871501cbb3b3c72d60473fa7ec73d135bbadf911`;
- immutable candidate source archive SHA-256: `f96427458902b8dd0eaefb2254b35245f3e7131cb9d5797c41dcd453fc1519a7`.

I authorize only these two MDM-owned paths:

1. `services/Diten.MdmService/src/Diten.MdmService.Api/Security/PlatformServiceTokenValidator.cs`
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/PlatformServiceTokenValidatorTests.cs`

After normal signature, issuer, audience and lifetime validation, the validator shall inspect the raw signed JWT payload. Each `iat`, `nbf` and `exp` member must occur exactly once, have JSON Number type and be representable as the existing signed 64-bit seconds value. Digit-only JSON strings, fractional values and values outside the existing signed 64-bit range fail closed. The already approved exact-cardinality, audience, `iat == nbf`, configured skew, expiry and maximum-lifetime rules remain unchanged. No identity or authorization decision may be derived from an unsigned payload.

A different verifier may apply these exact bytes to the approved predecessor source and run native .NET 8 verification. This decision does not authorize Carrier UI, gateway, permission, broader MDM access, rollout, commit, push or stash.

Status: **UNAPPROVED until the owner sends this exact hash-bound decision or an equivalent decision covering the same bytes and behavior.**

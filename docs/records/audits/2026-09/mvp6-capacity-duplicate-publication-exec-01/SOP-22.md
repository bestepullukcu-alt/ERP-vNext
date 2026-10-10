# MVP6-CAPACITY-DUPLICATE-PUBLICATION-EXEC-01 — SOP §22

## Verdict

**PUBLISHED / PASS.** The owner-approved two-target SANDOP-CAPACITY v3 patch was applied by the single publication owner after the guard activation passed.

## Exact authority and artifacts

- Owner decision: `docs/records/decisions/2026-09/mvp6-capacity-duplicate-final-owner-decision-01.md`, SHA-256 `80c343e6ad07c053c69aa1c9de003396bf2fd347379bf72e734264caef42f91b`.
- Publication patch SHA-256: `064827b512586bb17813a8e3703cc186f4a94295c590329674897ba31327aa23`.
- Canonical preimage YAML: `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`.
- Preserved v2 annex: `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`.
- Published v3 YAML: `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`.
- Published v3 annex: `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64`.

## Execution and verification

The baseline hashes and absence of the v3 annex were rechecked immediately before application. The patch passed disposable `git apply --check`, applied to exactly the two authorized targets, and reproduced the approved target bytes. The v2 annex stayed unchanged.

After guard activation, the same exact patch was applied to a disposable checkout first. Its target hashes matched and the complete DocsPathGuard class passed 39/39; TRX SHA-256 `5498015996c5862ba1e720fa9be6ed72ccec7f4bea7e22d7d399982cb6f0b7dc`. The real checkout application then reproduced the same target hashes and its post-publication production-mode DocsPathGuard passed 39/39; TRX SHA-256 `34d593bf52763664f608cdc55bf71428ba70b4e8ef046495ab4ecd90ff059420`.

The independent final contract verifier remains bound at `docs/records/audits/2026-09/mvp6-capacity-duplicate-final-ver-01/`; its technical artifact checks were not repeated because the artifact bytes are unchanged. The formerly blocking guard condition is superseded by `mvp6-capacity-guard-single-activation-01` and the green post-publication TRX above.

## Changed files

1. `docs/analysis/contracts/sandop-capacity.openapi.yaml` — updated to approved 3.0.0 / wire v1 bytes.
2. `docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md` — new approved v3 annex.

No other canonical file changed. No pack promotion, runtime uptake, Program.cs, gateway/UI/shared permission, rollout, commit, push or stash was performed.

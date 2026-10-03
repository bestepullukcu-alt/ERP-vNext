# MVP6-SANDOP-CAPACITY-PUBLICATION-EXEC-01 — SOP §22

**Verdict: BLOCKED at the publication verification gate; exact canonical bytes were already applied by the prior single-writer publication task.** This WP did not apply the patch again. The current canonical YAML and annex match the owner's exact target hashes. The prerequisite production DocsPathGuard test remains red, and the published annex still calls itself `UNAPPROVED`/noncanonical. Neither issue is waived or silently corrected. [Publication handoff](PUBLICATION-HANDOFF.md) is therefore hash-bound and marked **HOLD** for MOD-0190/MOD-0192 downstream action.

The requested **controls-PASS-before-apply** sequence cannot be certified retrospectively: the earlier writer applied the canonical bytes, then measured the failing guard. This successor preserves those authorized bytes and records the gate failure; it does not describe the earlier sequence as a successful gated publication.

## Authority and baseline

- Branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; staged index empty; existing dirty work retained.
- The owner's immediately preceding message gave three separate real decisions: MOD-0190 consent, MOD-0192 consent, and one-writer canonical publication authority, all against final-pack-01 YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`, annex `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442`, and patch `0ebc6a6fa6a867170b1d32545295e88676d74fb47794879a671c9f6c0375bd04`. The [earlier authority close](../mvp6-sandop-capacity-final-authority-close-01/AUTHORITY-MATRIX.md) binds the original 2.0.0/wire-v1 preparation and outside-consumer inventory decisions. Its historical OPEN rows are superseded by the subsequent real owner message; the old record is unchanged.
- [Final-pack-01 manifest](../mvp6-sandop-capacity-final-pack-01/MANIFEST.tsv) pins canonical preimage SHA-256 `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`. `git show HEAD:docs/analysis/contracts/sandop-capacity.openapi.yaml` still yields that exact preimage. Current canonical YAML instead has the authorized target hash, so a second real-checkout `git apply` would be wrong. The new annex is already present at its target hash.
- The [prior publication report](../mvp6-sandop-capacity-publication-01/SOP-22.md) records the actual one-writer application. Its `SHA256SUMS`, final-pack-01's `SHA256SUMS`, and the independent final VER `SHA256SUMS` all verify against currently accessible bytes. This WP neither repairs nor rewrites them.

## Verification

| Check | Result | Evidence |
|---|---|---|
| Exact artifact and canonical hashes | PASS: current canonical YAML/annex are byte-identical to final-pack-01 targets; patch hash unchanged | [MANIFEST.tsv](MANIFEST.tsv) |
| Baseline and disposable patch | PASS: HEAD canonical SHA-256 equals package preimage; `git apply --unsafe-paths --check` and disposable `git apply --unsafe-paths` exit 0; both disposable targets equal current canonical files byte for byte | Fresh command output in this WP; prior raw checks in [publication-01 archive](../mvp6-sandop-capacity-publication-01/raw-evidence.tar.gz) |
| OAS 3.1/ref/examples | Exact target content inherited from independent [final VER](../mvp6-sandop-capacity-final-ver-01/SOP-22.md): full meta-schema/full validator 0 errors, 12 operations, refs/examples; prior publication recorded fresh 204 ref encounters and 98 response example checks | Hash-bound, not a new full-validator run |
| Production DocsPathGuard | **FAIL:** prior fresh run 38 PASS / 1 FAIL; `NoCodeFilePointsIntoDocsOutsideTheFiveFolders` cites historical JSON/Python `docs/analysis` references; neither published target is an offender | Prior publication TRX in raw archive; guard source/authority hashes in [MANIFEST.tsv](MANIFEST.tsv). Their modification times, and those of scan-relevant JSON/Python files under the affected audit tree, predate the prior run. No unchanged-test rerun. |
| Full architecture | **FAIL:** prior fresh run 52 PASS / 4 FAIL: DocsPathGuard; two HCM/Talent JWT clock-skew guards; Platform Mongo per-run DB guard | Prior full TRX in raw archive; no waiver and no repeat without changed inputs |
| Protected input / diff | PASS: DEMAND hash unchanged; scoped `git diff --check` 0; no extra canonical file changed by this WP | [MANIFEST.tsv](MANIFEST.tsv), `git status --short` |

## Exact blockers and narrow disposition

1. **DocsPathGuard gate:** The production scan fails on existing `.json`/`.py` references in candidate, historical VER and MOD-0186 HTTP composition records. The prior TRX contains exact path:line offenders. The guard source and authority inputs are unchanged at the hashes in this record. An exact ownership/provenance disposition of those files is required; this publication WP cannot create a blanket exclusion, rewrite historical evidence, or claim guard PASS. The rule is not expanded merely because SANDOP is now canonical; SANDOP is absent from current guard `canonicalTargets`.
2. **Annex status contradiction:** [canonical annex:1–3](../../../../analysis/contracts/sandop-capacity-semantics-v2.0.0.md) still says `proposed`, `UNAPPROVED`, noncanonical and unpublished. Its byte hash is exactly the one the owner approved, so this WP cannot edit it without changing the release artifact and obtaining an exact new-hash disposition. The owner's real publication decision establishes application authority but does not make this documentary contradiction disappear.
3. The other three architecture failures concern HCM/Talent JWT skew and Platform Mongo test DB naming. They are outside this contract publication scope. They remain red; no new waiver is created.

## SOP §22 handoff fields

| Field | Result |
|---|---|
| Agent verdict | `BLOCKED` for the requested **controls-pass publication gate**; canonical target bytes present from prior authorized writer. |
| Changed files | Only this new audit directory: `SOP-22.md`, `PUBLICATION-HANDOFF.md`, `MANIFEST.tsv`, `SHA256SUMS`. No canonical mutation in this WP. |
| Golden/contract flow | Frozen baseline → exact final-pack-01 two-file patch → current canonical target hashes, confirmed by prior application and fresh disposable replay. |
| Sub-flows / failure paths | Guard and annex narrative blockers remain. No second apply, waiver, inferred guard binding, or unrelated source fix. |
| Tests | Fresh hashes and disposable apply PASS. Prior content-bound DocsPathGuard 38/1 FAIL and architecture 52/4 FAIL; no unchanged-test repeat. |
| Persistence / security / audit / observability | No runtime/DB action. E1/E2 contract evidence only. |
| Migration / rollback | None; no rollback of authorized canonical bytes. Preimage is pinned in HEAD and package manifest. |
| Decisions | Owner's three exact decisions are bound; no pack/Phase 1.5/runtime/Program.cs/rollout authority inferred. |
| Known gaps | Guard disposition, annex status correction under a new exact hash if chosen, consumer repin and later runtime uptake. |
| Out-of-scope changes | None; no commit, push or stash. |

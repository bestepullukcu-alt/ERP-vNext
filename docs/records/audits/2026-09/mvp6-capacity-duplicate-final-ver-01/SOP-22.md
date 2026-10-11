# MVP6-CAPACITY-DUPLICATE-FINAL-VER-01 — SOP §22

**Verdict: BLOCKED for canonical publication.** The approved exact final artifact is
internally valid and the two-file publication patch is byte-deterministic, but the
required current DocsPathGuard gate is RED in both the production checkout and the
post-patch disposable snapshot. The owner decision says a failed guard blocks
publication and grants no guard mutation or exception. Therefore this verification
does not declare the patch publishable and no canonical file was changed.

## Authority and exact binding

The append-only owner record
`docs/records/decisions/2026-09/mvp6-capacity-duplicate-final-owner-decision-01.md`
has SHA-256 `80c343e6ad07c053c69aa1c9de003396bf2fd347379bf72e734264caef42f91b`
and binds the four approved decisions to exactly:

| Artifact | Required and observed SHA-256 |
|---|---|
| Final SANDOP-CAPACITY 3.0.0 / wire v1 YAML | `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab` |
| Final v3 annex | `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64` |
| Two-target publication patch | `064827b512586bb17813a8e3703cc186f4a94295c590329674897ba31327aa23` |

The release-prep package's 23-entry `SHA256SUMS` verified without mismatch. The
owner record is design-consumer consent for MOD-0190 and MOD-0192 and conditional
single-writer publication authority. It is not duplicate-name production-source,
guard, runtime, pack, E5/G5 or git authority.

## Preimage and disposable application

The production checkout and the fresh snapshot
`/private/tmp/mvp6-capacity-duplicate-final-ver-01-lwl8BC` began with:

- canonical YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`;
- v2 annex `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`;
- no `sandop-capacity-semantics-v3.0.0.md` target.

Patch dry-run and real apply both succeeded. The resulting disposable YAML and v3
annex are byte-identical to the approved targets. The v2 annex remained exact. A
reverse-to-preimage, independent validation run and re-apply reproduced the same
bytes, so partial or order-dependent publication was not observed.

The A candidate, B candidate and reconcile history trees were compared file by file
between production and the post-patch snapshot. Their respective 30-, 6- and 7-file
aggregate manifests remained equal (`b0320420…`, `914431e8…`, `8e5a98bc…`).

## Contract verification

The verifier reran the complete final checker from the verified package against a
restored preimage and disposable apply:

- 33/33 checks PASS;
- full OpenAPI 3.1 validation PASS;
- 204 local references resolved;
- 53 examples validated;
- six negative controls rejected bad OAS, missing title, broken ref, missing error
  code, wrong wire version and malformed correlation;
- strict old-code rejection and final allowlist acceptance were kept separate from
  the permissive string schema;
- only the approved two target files were produced and v2 was preserved.

These are contract/model results. They do not establish producer uptake, duplicate
race implementation, HTTP/runtime behavior, rollout or E5/G5.

## Blocking gate

The production DocsPathGuard result was **38 passed / 1 failed**. The exact failing
test is `NoCodeFilePointsIntoDocsOutsideTheFiveFolders`; it reports two unapproved
references in the already existing verifier evidence:

1. `docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01/raw/input-bindings-recomputed.json:46`
2. `docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01/raw/input-bindings-recomputed.json:53`

Both lines point to `docs/analysis`. The post-patch snapshot produced the exact same
38/1 result and exact same two offenders. Thus the proposed Capacity patch adds no
new DocsPathGuard offender, but the **current required gate still fails**. Baseline
RED cannot be converted into publication PASS.

The related full TenantArchitecture gate was also identical before and after the
patch: **52 passed / 4 failed**. In addition to DocsPathGuard, the existing baseline
contains one Mongo per-run database failure and two JWT clock-skew failures involving
Platform test helpers and HCM/Talent `Program.cs` files. Those three are outside this
contract task and were not changed. They reinforce the architecture-gate RED but are
not attributed to the Capacity patch.

## Disposition

The exact artifact/patch verification is PASS; the conditional release decision is
**BLOCKED** at the current publication/guard gate. A separately authorized owner must
dispose the two exact DocsPathGuard offenders (and decide whether the wider
architecture gate is itself a release prerequisite), then an independent verifier
must rerun the current gates. This task created no guard exception, authority record,
canonical publication, source change, pack promotion or git mutation.


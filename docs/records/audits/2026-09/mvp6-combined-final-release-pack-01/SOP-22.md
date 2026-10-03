# MVP6-COMBINED-FINAL-RELEASE-PACK-01 — SOP §22

Date2026-09-20. Single publication owner preparation; **REVIEWABLE FINAL PROPOSAL / UNAPPROVED / APPLICATION HELD**.
Branch feature/mvp6-logistics; HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c. Existing dirty/concurrent state preserved. Only owned output directory and disposable workspace written.

## Exact publication recommendation

SHIPMENT-BUNDLE **3.0.0 / wirev1**; rationale and candidate→final delta in COMPATIBILITY-DELTA.md. This is a recommendation, not an approved release. Prepared YAML has intended FROZEN metadata for the proposed publication; it is stored only here/disposable, not canonical. Three annex destinations versioned consistently; existing Loads/Carrier annexes unchanged dependencies, not republished replacements.

|Proposed canonical destination|Final proposed SHA256|
|---|---|
|`docs/analysis/contracts/shipment-bundle.openapi.yaml`|`5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`|
|`docs/analysis/contracts/shipment-root-semantics-v3.0.0.md`|`7d1327a12b9775a594631dd9eb3c3c4c90e7f8429581f9ff7f42dde419f7b8af`|
|`docs/analysis/contracts/returns-semantics-v3.0.0.md`|`00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11`|
|`docs/analysis/contracts/claims-semantics-v3.0.0.md`|`16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63`|

Publication patch `944a228d076807d643fa1ce714a982aab2e8438e064aea3479a90b4e11b75396`. Baseline existing canonical93c696…3571; new annex destinations ABSENT. Publication patch applied only in disposable workspace, four outputs byte-equal. candidate-to-final.patch exposes every metadata/path/packaging label change.

## Input/evidence binding

Verified input candidate YAML `d763b571a1c6b88cbfadc56af2c51da1c0009589e3b82bcbe122329b17213c88`, independent verification report and376-file evidence archive were hash-checked. Previous verifier archive SHA256 `acb59e32330ca86dbcf701158baba84aedbab8dc567ce44ef9bc767b10baa855`; report SHA256 `4a65264bf3559fec9e3e0fbf1b155fe0de1dafb7f6833d682af8dfbe961e1fb9`. Actual Root/D186/D187 design/candidate approvals remain valid within their stated scope; no business reapproval requested.

Fresh targeted checks: full OpenAPI3.1 validator0.7.2 zeroerrors;298localrefs;236examples;5annex links resolve; four reversible metadata transformations recover original bytes; normalized operation AST equality; shared components/webhooks unchanged. No model suites rerun: prior126 independent checks,293Returns checks,35Claims checks/7mutants and68combined checks are **historical for this step**, transferred only through exact unchanged semantics proof. No new HTTP/Mongo/JWT/consumer uptake claim. Raw validation and exact command evidence in archive.

## Guard binding — calculation, not activation

Real System.Text.Json GetRawText+UTF8+SHA256 algorithm copied byte-exact from existing payload-generator.cs.txt and matching production ReadAuthority. Generated payload **85154a3e3368bfe187447bde269d0041a5863f3ef8b57b8e4397de97635cb270**,11185bytes. No JSON canonicalization, no BOM/trailingLF. Restamping draft decision file hash into authority envelope produced identical payload. Production ReadAuthority itself was NOT run as authorized-success: it must reject UNAPPROVED; no synthetic decision was manufactured.

Authority draft SHA256 `5fb3b8c92ee2ce42e79953700960c3f20ec71dbdad3b926eb1213dffbd2f525e`; decision draft SHA256 `9a1214f8f6440832ee306de9e481ddf5cdaa07953a6b102ab3be5d42e49d3a3b`. Both UNAPPROVED. Proposed decision destination: docs/records/decisions/2026-09/mvp6-combined-final-release-guard-owner-decision-01.json. After real consent, final stamped decision FILE hash must replace draft pointer; final authority hash changes and is reported. Payload arrays stay exact.

Guard canonicalTargets intentionally remain the existing two used targets (YAML updated; Carrier unchanged). Reader rejects unused canonical targets. New annexes are sealed by publication manifest and direct link/hash validation; no invented active-tool seal or unused target was added. All22existing seal objects are identical to reviewed R2 disposition, first17identical to live authority; all source/provenance hashes freshly checked. Previous approved634208…42202 payload and R2unapprovedb1dd34…364c1 do not authorize this85154a…cb270 payload.

Existing R2 policy, boundary-test, rule, fixture and byte-identical relocation changes remain proposed, individually identified in OWNER-DECISION-TEXTS.md. This package's guard-policy-fixture-relocation.patch is exact retained R2 final.patch sections, excluding superseded authority/decision sections; it changes no new policy. Do NOT apply old R2 final.patch plus these replacements indiscriminately. Guard binding UNAPPROVED patch is review staging only, not a runnable activation instruction. Do not combine aggregate and component patches twice.

Historical R2 guard results were38PASS/1expected synthetic production rejection, with3other architecture failures; these were not rerun or promoted. Current affected guard/rule/working-artifact baseline hashes still match R2. Production readiness requires future real approval binding and coordinated publication/guard tests; no existing test failure is waived here.

## Single-owner next operation order

1. Review separate A consumer consent, B publication, C1policy/C2fixture/C3binding activation scopes in OWNER-DECISION-TEXTS.md; each needs actual accountable decision. No same-message approval implied by this report.
2. On actual grants, record schema-valid human decision; stamp final file hash into authority while preserving arrays/payload. Hashes verified anew; no synthetic APPROVED record.
3. Prepare disposable current checkout with exact proposed publication, reviewed policy/fixture/relocation and approved binding together. Run real production ReadAuthority and full DocsPathGuard; report other suite failures separately. No blanket bypass.
4. If gates pass, recheck live baseline/intervening changes; one writer applies only approved targets. Record final target/decision/authority/seal hashes. Roll back only own changes if application fails. Commit/push/stash remain unauthorized.
5. Canonical uptake, root producer/consumer runtime, Loads multi-root disposition and operational rollout remain separate work. Publication/contract consent is not migration/backfill/DEV GO.

## SOP22 boundaries

Golden flow: exact proposed final publication bytes + reversible delta. Subflows: annex link relocation, decision hash envelope stamping. Failure paths: UNAPPROVED status cannot activate, payload/target/baseline mismatch failclosed. Tests: metadata/schema/ref/example targeted checks only. Persistence/security: no runtime test or operational DB. Audit/evidence: source pins, patch, manifest, raw logs, reader payload and22seal check. Observability: recorded outputs; no services started. Migration/rollback: none applied; future rollback limited to own approved publication. Decisions: proposals only. Blockers: missing final consumer/publication/guard scopes and future production-mode combined validation. Known gaps: no runtime uptake or rollout readiness; historical tools/major-version consumers require explicit disposition. Out-of-scope changes:none.

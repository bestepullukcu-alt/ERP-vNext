# MVP6-SANDOP-OAS31-VALIDATOR-01 — SOP §22

**Technical verdict: PASS for full OpenAPI 3.1 document validation of both exact candidates.** This is E2 syntax/structure evidence only. It grants no policy, compatibility, publication, consumer or runtime approval.

Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; checkout already had 175 dirty/untracked status rows at preflight. Agent-owned output is only this directory. Candidate/canonical/pack/runtime/dependency/lock files were not edited; no git mutation. Repo-wide no-change is not asserted during parallel lanes.

## Tool provenance and schema

The prior independent [combined-final-release VER](../mvp6-combined-final-release-ver-01/SOP-22.md) reports a successful `openapi-spec-validator 0.7.2` full OpenAPI 3.1 run. Its archived `evidence/verifier-checks.py` calls `OpenAPIV31SpecValidator(document).iter_errors()` with read-only prior disposable `tooldeps`. This run reused that exact installed validator family at `/private/tmp/mvp6-combined-final-release-q16pebui/tooldeps` (no install/network). Python `3.9.6`, PyYAML `6.0.3`, jsonschema `4.25.1`.

Offline meta-schema: `/private/tmp/mvp6-combined-final-release-q16pebui/tooldeps/openapi_spec_validator/resources/schemas/v3.1/schema.json`, SHA-256 `e7cb616a2a10849a166c4e4a93c62c56cfea02cc00eadf287e2fb875e7124098`. The script runs **both** the packaged complete document meta-schema validator and `OpenAPIV31SpecValidator` (meta-schema plus library semantic checks), not only local refs/examples. No remote schema resolution was required. The only stderr line is a `urllib3` LibreSSL compatibility warning; it did not affect exit status.

## Exact execution and results

Working directory: `/Users/natig/Projects/ERP-vNext-recovery`. Command (stdout/stderr captured separately):

```sh
python3 docs/records/audits/2026-09/mvp6-sandop-oas31-validator-01/validate_oas31.py > docs/records/audits/2026-09/mvp6-sandop-oas31-validator-01/raw-results.json 2> docs/records/audits/2026-09/mvp6-sandop-oas31-validator-01/raw-stderr.txt
```

Exit **0**. Raw results and diagnostics are archived alongside this report. The two deliberately invalid controls, `openapi: bad` and missing `info.title`, each produced one error in both validators for each candidate. This confirms the invocation actually applied document-level constraints.

| Candidate YAML | SHA-256 | Meta-schema errors | Full validator errors | Verdict |
|---|---|---:|---:|---|
| `mvp6-sandop-capacity-amendment-01/sandop-capacity.openapi.candidate.yaml` | `88070dc3fa27aff9ba0d4b40ba1a43641906f77d369e4ce1a84be75ae267816d` | 0 | 0 | PASS |
| `mvp6-sandop-capacity-amendment-candidate-01/sandop-capacity.openapi.yaml` | `aa6a1e1e238ad347e03dc793dfef8f6f7dad786ec0bb9d7871603f63f4d8a658` | 0 | 0 | PASS |

The successor VER may rerun the command above while this read-only tooldeps directory remains available. Its first preflight should verify the script SHA-256 `2dc7aadbd4a649e0fa8be4959673ea0e37844f1736281e35c40ef0f984a07d6b`, candidate hashes and schema hash; a missing disposable dependency path is an environmental blocker requiring the same pinned `openapi-spec-validator 0.7.2` in a new disposable environment, not a reason to relabel ref/example checks as full validation.

**Evidence boundary:** no patch application, candidate reconciliation, business-policy comparison, independent consumer harness, HTTP/JWT/Mongo, or release/guard execution was performed. The candidates are separate byte artifacts; this PASS does not choose between them. No migrations or rollback were needed. Changed paths: this report, validator script, raw JSON, raw stderr, and archive manifest only.

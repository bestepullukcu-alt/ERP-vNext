# MVP6-LOADS-ROOT-ACQUISITION-AMENDMENT-VER-01

Role: independent read-only contract verifier; do not modify the candidate or canonical files.

Verify the exact baseline, candidate YAML, candidate annex and amendment patch hashes recorded in `SOP-22.md`. Apply the patch to a disposable copy of canonical 3.0.0 and require byte identity with both candidate targets. Run full OpenAPI 3.1 meta-schema and `openapi-spec-validator` checks, resolve all local references, validate schema-bound examples, and execute missing/null/malformed/UUID/nil root fixtures.

Compare all operations and shared components. Permit only candidate metadata, the Loads annex pointers, the `queryLoads` example, `LoadSummary.lifecycleCorrelationId`, and the new versioned Loads annex. Verify that request schemas, `LoadResponse`, event/shared schemas, and Shipment/Carrier/Returns/Claims behavior remain unchanged. Check the annex against `D185-ROOT-ACQ-01`, including persisted Load root authority, always-emission when present, no derivation/backfill, fail-closed consumer behavior, tenant/LE/RBAC/replay preservation, and the unresolved multi-Shipment policy.

Produce a separate SOP §22 technical verdict and immutable evidence archive. Keep E1/E2 static evidence separate from runtime, consumer uptake, publication and rollout. Do not select a final version, grant consent, publish canonical files, promote packs, or start runtime DEV.


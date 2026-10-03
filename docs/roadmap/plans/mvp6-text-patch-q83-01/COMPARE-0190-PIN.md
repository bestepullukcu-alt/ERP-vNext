# COMPARE-0190-PIN — SANDOP-CAPACITY 2.0.0 vs 3.0.0 for the six MOD-0190 operations (Q83, finding F190-PIN)

Run 2026-09-26 ~18:22 +03:00 in the chat lane (VM /tmp, Python + PyYAML). Read-only; no contract edited.

| Input | Path | SHA-256 |
|---|---|---|
| 2.0.0 YAML (published copy; = §22 acceptance pin) | `docs/records/audits/2026-09/mvp6-sandop-capacity-final-release-pack-01/publication/docs/analysis/contracts/sandop-capacity.openapi.yaml` | `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` |
| 3.0.0 YAML (canonical) | `docs/analysis/contracts/sandop-capacity.openapi.yaml` | `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab` |
| 2.0.0 annex | `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` | `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` |
| 3.0.0 annex | `docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md` | `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64` |

Method: each operation's object (parameters, requestBody, responses, path-level parameters) with every internal `$ref` resolved recursively, serialized with sorted keys, compared byte-for-byte; error codes = every UPPER_SNAKE token inside the resolved object.

## YAML — six bound operations

| operationId | Method + path (same in both) | parameters | path params | requestBody | responses (all codes, schemas, examples) | error-code set | resolved-object sha256[:12] 2.0.0 / 3.0.0 |
|---|---|---|---|---|---|---|---|
| `createSandopPlan` | `POST /sandop-plans` (same) | same | same | same | same | same (9 codes) | `e1c511114c83` / `e1c511114c83` |
| `getSandopPlan` | `GET /sandop-plans/{sandopPlanId}` (same) | same | same | same | same | same (6 codes) | `4c5675bcae8b` / `4c5675bcae8b` |
| `captureSandopSnapshot` | `POST /sandop-plans/{sandopPlanId}/snapshots` (same) | same | same | same | same | same (12 codes) | `076c44749530` / `076c44749530` |
| `listSandopSnapshots` | `GET /sandop-plans/{sandopPlanId}/snapshots` (same) | same | same | same | same | same (7 codes) | `836347048d78` / `836347048d78` |
| `recordSandopSignOff` | `POST /sandop-plans/{sandopPlanId}/sign-offs` (same) | same | same | same | same | same (11 codes) | `36715238172a` / `36715238172a` |
| `listSandopSignOffs` | `GET /sandop-plans/{sandopPlanId}/sign-offs` (same) | same | same | same | same | same (6 codes) | `8776dcfcd65b` / `8776dcfcd65b` |

All other operation keys (summary, tags, description, security) are equal as well (no differing key found).

**Whole-file difference (all 8 differing leaves):** `info.version` (2.0.0 → 3.0.0), `info.x-semantics-annex` (v2.0.0 → v3.0.0 annex), and six leaves of `components.responses.CapacityPlanStateConflict` (Capacity `createCapacityScenario` 409 name conflict). None is reachable from the six S&OP operations.

## Annex (semantics) — full diff in `evidence/annex-v2.0.0-vs-v3.0.0.diff`

| Annex change | Touches the six S&OP operations? |
|---|---|
| Title and intro (version 3.0.0; "the sole behavioral delta from 2.0.0 is the `createCapacityScenario` duplicate exact-name 409"; executor provenance sentence) | No |
| `createCapacityScenario` operation-matrix row + new section "Successor-only createCapacityScenario duplicate exact-name disposition" | No (Capacity only) |
| Lifecycle paragraph: the `0190:` segment (first 338 characters) | **Byte-identical**; only the following `0192:` executor wording changed |
| Scope-limits paragraph: "Static candidate results" → "Static contract results" | No (wording in a shared paragraph; no rule change) |
| S&OP operation-matrix rows (v2 lines 23–28 = v3 lines 26–31) and transport line (v2 7 = v3 10) | Byte-identical |

## Verdict

**IDENTICAL** — operationIds, methods, paths, parameters, request and response schemas, examples and error codes of `createSandopPlan`, `getSandopPlan`, `captureSandopSnapshot`, `listSandopSnapshots`, `recordSandopSignOff`, `listSandopSignOffs` are the same in 2.0.0 and 3.0.0, and the annex's 0190 semantics are unchanged. P4 is therefore written.

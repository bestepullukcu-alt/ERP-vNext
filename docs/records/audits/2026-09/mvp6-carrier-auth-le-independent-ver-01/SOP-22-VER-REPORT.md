# MVP6-CARRIER-AUTH-LE-INDEPENDENT-VER-01 — SOP §22

Date: 2026-09-23  
Role: independent read-only verifier  
Branch: `feature/mvp6-logistics`  
HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **REWORK**

## Exact input binding

| Input | Verified SHA-256 | Result |
|---|---|---|
| Approved patch | `6559c94835814ab65dc50a05cca32db905e31e637f9a15aeeeb033e9850c83e6` | PASS |
| Ten-path source manifest | `84ce27833951bfed92edf46c02a0ee7c8934d1c6eff006e665d3ddaf09bf8b23` | PASS |
| Final source archive | `a10bb93246557475d6518975aa0ee1dd8928ca08781fc2fb0cfbb04136463388` | PASS |
| Build source set | `298eb741dcc33cb45eb6f9096a8c7d190e9ec47e77fd5cba032c7f13cce9956e` | PASS |

The final archive is gzip-valid, contains 2,507 entries, excludes development/local settings, resource forks, and `bin`/`obj` outputs, and contains the 17 transitive source projects needed for the two builds. Intermediate archive hashes are not controlling and were not used.

All ten target hashes match the manifest. The six modified paths in the real repository still match their approved preimages, and all four added paths remain absent. The verifier did not apply the candidate to the repository.

## Independent build provenance

The final archive was extracted to a unique disposable workspace. `BUILD-SOURCE-SET.sha256` passed in full before build.

| Component | Result | Binary SHA-256 |
|---|---|---|
| Auth API | PASS; 0 errors, 19 warnings | `dda60bc0735913f589730acec54bc08db0133ddccca80ed0a31b1ce3dd296059` |
| Platform API | PASS; 0 errors, 55 warnings | `4f6bfca430e9a28a6e5939245b345e29bff078ae1bea0e1d1b81ee0d362b1b3b` |

Warnings include the unavailable vulnerability-data endpoint (`NU1900`) and existing code warnings. They were not hidden or converted to a zero-warning claim. The projects target `net8.0`; the isolated runtime used the available .NET 10 host with major roll-forward. This runtime evidence establishes the blocker but is not represented as native .NET 8 acceptance.

## Independent runtime reproduction

An isolated Mongo replica set ran on port `37181`; Platform ran the independently built binary on port `18157`. Operational Mongo `27017` was not used. No usable internal key, JWT, signing key, bearer token, or connection string is retained.

| Request | Result | Interpretation |
|---|---|---|
| wrong internal key | HTTP `401` | internal-key rejection works |
| correct internal key | HTTP `400`, unresolved `TenantId` | request enters the endpoint but fails before tenant-scoped resolution |

This independently reproduces **GAP-CARRIER-AUTH-LE-01**. Because it is the controlling failure, zero/multiple/inactive/revoked scope variants were left **NOT RUN**, as were independent login, refresh, MFA and forced-password flows. The writer's real login/refresh results remain inherited, content-bound evidence: both return HTTP 200 and both omit `legal_entity_id`.

The source path also independently confirms **GAP-CARRIER-AUTH-LE-02**: the Auth internal request has no caller bearer; Platform only forwards a caller bearer if one exists; the MDM lookup endpoint requires authentication and `mdm.legal-entities.read`. This is source-path evidence because F1 prevents the runtime from reaching MDM.

## Acceptance disposition

The complete result matrix is in `ACCEPTANCE.tsv`. The bounded implementation is not acceptable because its central positive behavior cannot execute. Carrier's fail-closed missing-LE behavior is preserved because no Carrier source is in the ten-path delta.

Exact remaining work:

1. establish trusted tenant context for the internal scope-resolution call without treating client-selected LE as authority;
2. define and authorize the narrow server-to-server MDM validation channel needed during Auth login/refresh;
3. after rework, independently exercise single/zero/multiple/inactive/revoked scopes, tenant/actor isolation, dependency failure, login, refresh, MFA and forced-password issuance.

## Repository preservation and cleanup

- No production source, contract, Carrier UI, Program.cs, gateway, permission, pack, or earlier audit record was modified.
- Branch and HEAD remained `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- The repository was already heavily dirty from active lanes; source-specific preimage/absence checks are the controlling no-change proof.
- The verifier wrote only this audit directory.
- Isolated Platform and Mongo processes were stopped; ports `18157` and `37181` were released.
- No commit, push, stash, rollout, or Carrier E2E acceptance occurred.

## Evidence

Raw commands, build logs, source excerpts, runtime responses, hash checks, preservation checks and cleanup proof are bundled in `evidence.tar.gz`. `MANIFEST.sha256` binds all permanent outputs.

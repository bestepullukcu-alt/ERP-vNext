# MVP6-CARRIER-AUTH-LE-IMPLEMENT-01 — SOP §22 DEV handoff

Date: 2026-09-23  
Role: single Auth/Platform source writer  
Branch: `feature/mvp6-logistics`  
HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **REWORK**

## Authorized and applied scope

The user's exact owner decision is recorded in `OWNER-AUTHORIZATION.md`. The approved candidate was applied only in the isolated checkout `/private/tmp/mvp6-carrier-auth-le-candidate-V7EbGR/repo`.

- Patch SHA-256: `6559c94835814ab65dc50a05cca32db905e31e637f9a15aeeeb033e9850c83e6`
- Source manifest SHA-256: `84ce27833951bfed92edf46c02a0ee7c8934d1c6eff006e665d3ddaf09bf8b23`
- Source archive: `source-archive.tar.gz`
- Ten target hashes: 10/10 match (`raw-evidence.tar.gz::raw/applied-source-manifest-check.log`)

No approved source file was changed in the real repository checkout. The only repository writes from this lane are this immutable audit handoff and its evidence.

## Build and process provenance

| Component | Build | Binary SHA-256 | Runtime |
|---|---|---|---|
| Auth API | PASS, 0 warnings/errors | `83069b346888d41ee832af0aa5b6f9e5747fc7830f0e858fb86e7e4e54a68dea` | PID/path captured in `raw-evidence.tar.gz::raw/runtime-evidence/process-binary.tsv` |
| Platform API | PASS, 0 warnings/errors | `8a5d2777977776652793c11a43affe6217ec1f58e4a5bee7998c98c7af3045ca` | PID/path captured in `raw-evidence.tar.gz::raw/runtime-evidence/process-binary.tsv` |

The environment used a lane-specific Mongo replica set on 37081, Platform 18057, Auth 18056, and an evidence-only MDM contract mock on 18059. All four ports were released after the run (`raw-evidence.tar.gz::raw/runtime-evidence/cleanup.log`). No operational Mongo 27017 was used.

## Runtime result

The candidate builds but does not meet its core acceptance behavior.

1. Correct internal key reaches the new Platform endpoint, then fails HTTP 400 because `/api/internal` bypasses tenant resolution while the resolver uses tenant-filtered repositories.
2. Wrong internal key correctly returns HTTP 401.
3. Real Auth login and refresh both return HTTP 200, but decoded redacted claims show `legal_entity_id: null`.
4. The downstream MDM validation path also lacks usable service authorization during login/refresh. It was not reached because the tenant-context failure occurs first.

Full details and exact path:line evidence are in `FINDINGS.md` and `raw-evidence.tar.gz::raw/source-path-evidence.txt`.

## Test disposition

- Auth API build: PASS.
- Platform API build: PASS.
- Runtime internal-key negative: PASS.
- Real token LE positive: FAIL.
- Scope/boundary cases: blocked before resolver evaluation.
- Auth test project: NOT ESTABLISHED. The copied test project did not compile because existing test dependencies such as EphemeralMongo and WebApplicationFactory were unavailable/incompatible; see `raw-evidence.tar.gz::raw/auth-tests.log`. This is not reported as a product-test pass.
- Detailed matrix: `ACCEPTANCE.tsv`.

## Security and preservation

- No client-supplied LE was treated as authority.
- Carrier's missing-LE 403 was not changed.
- No usable token, JWT key, internal key, connection string or production secret is archived.
- No Carrier UI, gateway, permission, contract, Supplier, Program.cs or Lane B source was changed.
- No commit, push, stash or rollout occurred.

## Exact gaps

- **GAP-CARRIER-AUTH-LE-01:** authenticated internal endpoint does not establish tenant context before tenant-filtered resolution.
- **GAP-CARRIER-AUTH-LE-02:** login/refresh have no authorized server-to-server MDM lookup-validation channel.

The second gap is outside the approved ten-path scope. `REWORK-AUTHORITY-NEEDED.md` defines the narrow successor candidate boundary; it is not an approval.

## Handoff state

Writer work is complete and no source writer remains active. The candidate is **not accepted**. Independent verification must reproduce or confirm these findings without fixing source. Carrier E2E remains HELD.

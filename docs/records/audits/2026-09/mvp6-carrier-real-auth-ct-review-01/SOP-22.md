# SOP §22 — MVP6-CARRIER-REAL-AUTH-CT-REVIEW-01

## Verdict

**PARTIAL — the hash-bound isolated Carrier backend/UI/real-Auth functional slice is accepted; the bounded UI evidence gate remains open because the controlling durable-PNG criterion is not satisfied.**

No product defect or source rework is identified by this review. The open item is evidence completeness: no approved durable screenshot save/export mechanism produced the required PNG. Existing DOM, accessibility, browser-session and HTTP/DB evidence proves the functional rows but is not an authorized replacement for the explicit PNG row.

This decision does not grant full-module acceptance, common-checkout integration, rollout, E5/G5 or repository PASS.

## Authority and baseline

- Repository: `/Users/natig/Projects/ERP-vNext-recovery`
- Branch: `feature/mvp6-logistics`
- HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Initial dirty inventory: 2,800 porcelain entries; SHA-256 `c35ba9c1414071ec53e6377e1e73d8d7ce9816a2f0765239b33d25bf89c7b3dc`.
- Prior backend CT decision: `mvp6-mod0184-ct-accept-02-2026-09-18/README.md`; bounded C01–C12 E4 scope accepted, UI/gateway/shared permission/E5/G5 excluded.
- Controlling Auth handoff: SHA-256 `95dafe0fbfdfdf490f9ecdb57f65582d0146da435ad938f1df13c11105586cd2`.
- Final UI manifest: SHA-256 `3b7086f0cc839c33f0753076e732aa436b9a8e5cd186f1484f2b0225364d033c`; registered isolated UI worktree matches 21/21 paths.
- Final Auth/Platform/MDM manifest: SHA-256 `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`; source archive SHA-256 `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd` matches 22/22 paths.
- Build input manifest: SHA-256 `516ba828bed602e4355a5cc6871501cbb3b3c72d60473fa7ec73d135bbadf911`; producing lanes recorded 3,333/3,333 matches.

The common checkout does not contain the accepted isolated UI and Auth source sets. This review therefore accepts only the immutable, hash-bound isolated evidence set. It does not claim those bytes are integrated in the common checkout.

`mvp6-effort-carrier-e2e-update-05` was read and its checksum package verified, but it is reporting context only. Its hours, percentages and delivery labels were not used to prove or upgrade any acceptance row. The technical verdict below derives from the exact source, binaries, processes, browser observations, HTTP/DB records and previously sealed technical evidence.

## Evidence provenance

The E2E archive SHA-256 is `c5df26ab200d4353be52c9ef513fa6bc777aef9ab2f092fa32af8e718f7435f1`. Its runtime manifest binds fresh Release binaries to the launched processes:

| Component | Binary SHA-256 | Process binding |
|---|---|---|
| Auth | `7d766fdc84e4a21bfc4d29d467eae02e121b51b8c63d294ca1cdd854b60ccf8b` | `127.0.0.1:5656` |
| Platform | `0db7fb8915c987c397dafe0f2176685e08b361738ce53df86696d8b177b75f40` | `127.0.0.1:5657` |
| MDM | `2377ddb569bc402c49cb970b56e2a38c68f95a8a201ed9dcc078ead3f82d24c2` | `127.0.0.1:5659` |
| SupplyChain | `27d1c0b5d0c5eb4c9936eca2cb9c996d9aad84f423ca9c64039cb03dd683a37f` | `127.0.0.1:5661`, restarted once |
| Web | `61073625bce47057c31900c5d57f465f024f025d65a6974df2a9c16c0d7d8702` | `127.0.0.1:5601` |
| Gateway | `02eb5653dcaae4712057cd37a7d75d19aa8ea722bd9acf6216676a5ac86402e2` | `127.0.0.1:5600` |

The lane used native .NET 8 Release builds and DB-010 replica set `rsCarrierRealAuthExec01` at `127.0.0.1:38994`; operational port 27017 was not used. The archive records request/response, persistence, listener and cleanup evidence without reusable credentials.

## Successor acceptance

`SUCCESSOR-ACCEPTANCE.tsv` is controlling for this review.

- **Fresh E2E PASS:** real Auth browser session through Web/Gateway; list; create; exact replay and changed-payload conflict; persisted-row status offcanvas; reload and restart persistence; read-only RBAC; UAS-001; tenant and legal-entity isolation; missing signed LE rejection; refresh-time LE revocation.
- **Inherited/content-bound:** backend C01–C12 CT acceptance; F-01/F-02 independent 49/49; seven-language 70/70; 768/390 responsive measurements. These are not relabelled as fresh E2E runs.
- **OPEN:** durable PNG evidence.
- **Observation:** Platform aggregate `/health` returned 503 because `business_reference_data_provider` was unhealthy. The required Auth→Platform→MDM calls succeeded, while self/Mongo/Hangfire were healthy. This remains an environment observation and is not a Platform aggregate-health PASS.

## PNG disposition

The durable PNG row is part of the controlling successor acceptance (`RA2-18` / `SUC-19`) and is therefore mandatory for a complete bounded UI evidence verdict. The supported browser surface returned screenshot bytes for inline display but exposed no approved durable screenshot save/export operation. Page export, page assets, data-URL extraction, CDP, base64 extraction and native capture are not authorized substitutes.

Result: **OPEN; no waiver; no product rework requested.** Closure requires a supported screenshot artifact-save/export capability and a new evidence-only verification bound to the unchanged final UI manifest. If the source manifest changes, the PNG must be produced against the successor manifest.

## Accepted scope

Accepted within the immutable isolated source/evidence boundary:

1. The earlier bounded MOD-0184 backend C01–C12 behavior.
2. Carrier UI list, create, replay/conflict and status transition from the persisted row.
3. Restart persistence for the created and replayed carriers.
4. Real Auth-issued browser/API session handling through Gateway.
5. RBAC, UAS-001, tenant isolation, legal-entity isolation and fail-closed missing/revoked LE behavior.
6. The exact fresh and inherited distinctions recorded in the successor matrix.

## Excluded and open gates

- Durable PNG remains open and prevents complete bounded UI evidence acceptance.
- Common-checkout integration and any source transfer remain open; accepted isolated source bytes are not present in the root checkout.
- Shared Search localization remains outside this Carrier-owned decision.
- Platform aggregate health remains observed as 503; no aggregate-health acceptance is issued.
- Gateway/shared permission catalog changes, operational rollout, migration, E5/G5, repository-wide architecture PASS and full-module completion remain excluded.

## Verification performed by this CT review

- Recomputed both controlling package checksum manifests: PASS.
- Recomputed final UI source against the registered isolated worktree: 21/21 PASS.
- Recomputed final 22-path source against the immutable source archive: 22/22 PASS.
- Inspected the archived source→binary→process bindings, HTTP/DB evidence, browser matrix, cleanup and secret scan.
- Reused unchanged test results by hash; no build, runtime or browser tests were rerun.
- Verified the effort-report package separately and excluded it from the acceptance basis.

## Final disposition

**PARTIAL.** Functional bounded Carrier backend/UI/real-Auth E2E evidence is sufficient and accepted at its stated E4 boundary. Complete bounded UI acceptance remains blocked only by the mandatory durable PNG evidence row. No product rework, rollout or downstream approval follows from this decision.

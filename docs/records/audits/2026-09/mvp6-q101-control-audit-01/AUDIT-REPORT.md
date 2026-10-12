# Q101 — MVP6 control and rule-compliance audit (read-only) — AUDIT-REPORT

```text
VERIFICATION REPORT (SOP §37)

WP ID:              WP-MVP6-AUD-101 · Prompt Q101 v1 · Lane AL-MVP6-AUD-101 (INS)
Verifier:           read-only-auditor (/read-only-audit, strict repository-read-only), with code-quality-agent
                    and security-agent rule knowledge; Cowork chat lane on the Linux VM bridge (owner decision ~21:25)
Verification date:  2026-09-26 · start 21:26:56 +03:00 (18:26:56Z)
Branch/HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (start and end)

Agent Verdict:        AUDIT COMPLETE — 16 findings (4 HIGH, 7 MEDIUM, 5 LOW, 0 BLOCKER); no secret value printed
Verification Verdict: n/a (inspection, Profile C)
CT Status:            returning to CT

Evidence level achieved: OBSERVED (static repository measurement; hashes, greps, parsers, one static verifier run in /tmp)
Required evidence level: OBSERVED (Profile C); runtime rows need the Mac (listed below)

Checks:
- scope:            Groups 1–3 fully inventoried (5 + 27 + 101 items); Group 4 at hash level (36 archives)
- build:            NOT RUN (no .NET here) — needs Mac
- tests:            NOT RUN — needs Mac; architecture-guard regexes replayed statically (0 offenders in scope)
- runtime:          NOT RUN — needs Mac
- persistence:      static only: Carrier/Loads repositories filter TenantId + LegalEntityId (+ IsDeleted=false for reads)
- RBAC:             static only: every Carrier/Loads action carries a permission attribute; PKS-001 keys (F09)
- tenant:           static only: scope filters present (CarrierRepository.cs:12-13, LoadRepository.cs:11-12)
- concurrency:      not assessed (needs runtime)
- idempotency:      not assessed (needs runtime)
- audit/evidence:   records cross-referenced for every item (INVENTORY.tsv)
- observability:    no token/secret logging found in SupplyChain src (grep)
- migration/rollback: n/a
- integration:      working tree ≠ accepted integrated composition (F01); UI integration only as proposals (F07)
- console/security leakage: no inline handlers, native dialogs, browser storage, cookie/Bearer handling or direct
                    service-port calls in overlay JS/cshtml; one static secret in a product test path (F02)

Failed criteria:
- F01 integration state, F02 secret literal, F03 missing Loads manifest provider, F04 add-module Phases 5–6 (HIGH)

Rework required:  yes (see FINDINGS.tsv; est. 80 / 145 / 244 h O/M/P, all findings)

Next gate:        CT disposition of F01–F16 → fix WPs dispatched through @orchestrator (+ /add-module) per SOP §36
```

## 1. Summary counts by group and status

| Group | Items | Status |
|---|---:|---|
| G1 draft overlays | 5 | 5 CT ACCEPTED as DRAFT (not writer-complete); all Cowork-Linux chat lanes |
| G2 evidence kit | 27 | 27 UNVERIFIED (installed 27/27 hash match; VER re-check Q67 passed; K00–K11 not run) |
| G3 product code in the working tree | 101 | 92 ACCEPTED bounded (0183 root R2, 0184 Carrier, 0185 Loads; each equals the accepted A12-360 manifest hash) · 1 ACCEPTED by chain (DocsPathGuardTests.cs) · 1 SUPERSEDED (SupplyChain Program.cs) · 7 generated TRX |
| G4 source archives since 2026-09-17 | 36 | 13 referenced by a CT verdict · 17 referenced by VER records only · 6 with no CT/VER record found |

Environment: G1 and G2 were produced in Cowork-Linux chat lanes. G3 was produced on the Mac: the TRX `computerName` is a macOS host,
the paths are under `/Users`, and the dotnet/Mongo runtime evidence ran there. G4 is mixed: 9 archives carry macOS AppleDouble
entries, 5 are the Cowork-Linux draft overlays, and 22 cannot be placed from the archive itself (unknown at hash level).

| Severity | Count | IDs |
|---|---:|---|
| BLOCKER | 0 | — |
| HIGH | 4 | F01, F02, F03, F04 |
| MEDIUM | 7 | F05–F11 |
| LOW | 5 | F12–F16 |

## 2. Top risks

1. **F01 — The working tree is not the accepted product (HIGH).** 92 of 101 files equal the accepted A12-360 successor manifest. Program.cs is still the
   MOD-0185 DEV-03 composition. Of the 360 accepted files, 53 differ and 215 exist only in archives: the Claims, Returns, S&OP and Capacity backend, the Shipments UI, and the
   ModuleRegistration providers. Building, testing or committing the working tree would therefore produce an unaccepted composition, and an integrated
   PASS (K18) is impossible until one INT WP composes the accepted source.
2. **F04 / F05 — Control gaps (HIGH / MEDIUM).** add-module Phases 5–6 were never dispatched for any MVP6 module. Prompts before Q93 v2 lacked the
   §17.1 metadata, the §17.3 pattern and the §17.4 entry points, and no Group 1/3 record shows `@orchestrator` + `/add-module`.
3. **F03 — MOD-0185 has no self-registration provider anywhere (HIGH).** Neither the working tree nor any of the 36 archives contains one (BLOCKER for module closure).
4. **F02 / F11 — Secrets (HIGH / MEDIUM).** A static probe signing secret sits in `services/…/tests/loads/runtime_probe.py:7`. Archive-level secrets are
   known from Q69. Values are not reproduced here.
5. **F06 / F07 — The UI drafts cannot pass their gates yet (MEDIUM).** The static `verify_datatable_page.py` run fails. Some failures are pack-OUT rows; the
   `@model` gaps and the Reset/Save View gaps look real. Gateway routes, nav keys and Program.cs registration exist only as proposals.

## 3. What needs the Mac to confirm

| Item | Why the Mac | Linked finding |
|---|---|---|
| `dotnet build` / `dotnet test` of the composed accepted successor (A12 360 + BC-SOURCE + Auth 22) and of each draft overlay | no .NET SDK in this lane | F01, F06, F07 |
| Architecture suite (DocsPathGuard, MongoTestDatabaseGuard, JwtClockSkew…) on the composed tree | the guards walk the whole repo, including docs/ .cs | F01, F15 |
| Loads suites before and after a behaviour-neutral reformat (33/33 + TRX) | proves F08 changes nothing | F08 |
| Runtime (Phase 4.5, §25 freshness) of Claims, Returns, S&OP, Capacity UIs through Gateway 5000 | needs Mongo + Chromium + services | F06, F07 |
| Evidence kit K00–K11 (Q24b) | Darwin gate; Mongo/dotnet | F10 |
| Permission attribute convergence regression (401/403 contract bodies) | runtime RBAC | F09 |
| Loads manifest provider + completeness tests | build/test | F03 |

## 4. Method (short)

The audit ran through the following steps:

1. Preflight baseline: branch, HEAD, 19 diff paths, and the untracked list under services/tests/scripts/frontend/gateway (127 lines).
2. Every G1–G3 file was hashed and cross-referenced to records by path and hash:
   - accepted manifests: SUCCESSOR-360, FINAL-355, FINAL-354, source-final-341;
   - DEV change manifests;
   - CT verdict and decision records;
   - CT-QUEUE and MILESTONE-EVENTS.
3. Static rule scans over the overlay and Group 3 code:
   - inline handlers, native dialogs, browser storage, token handling, hard-coded ports and secrets;
   - resx language/key parity and English placeholders;
   - PageDescription;
   - the permission-key format;
   - tenant filters;
   - line length.
4. The DocsPathGuard and MongoTestDatabaseGuard regexes were replayed over the scope.
5. `verify_datatable_page.py` ran on a /tmp copy of `frontend/Diten.Web` with the four UI overlays applied.
6. The archive listings were read with `tar -t`: AppleDouble entries and manifest-provider presence.

Details: README.md.

## 5. No-change verification

See README.md §No-change for the end-of-run comparison (branch, HEAD, 19 diff paths, untracked list, no index.lock, `git diff --check`).

Agent PASS ≠ CT ACCEPTED — returning to CT.

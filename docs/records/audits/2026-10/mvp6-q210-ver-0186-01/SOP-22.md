# Q210 — VER MOD-0186 Reverse Logistics (Returns): code in tree vs pack · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q210 · `AL-SCM-VER-0186` (VER) · required E1 (static) — **reached: E1**. Nothing built, no test run. |
| CT-QUEUE row | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv:306` |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` (mode **strict**: no fetch, no pull, no `.git` write; no fixes) → Phase B `documentation-writer` (this folder only) |
| Scope | `services/Diten.SupplyChainService/src/*/Features/Returns/` · `…/tests/Diten.SupplyChainService.Tests/Returns/` · the MOD-0186 pack |
| Pack under test | `execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md`, 868 lines, sha256 `933e89262713…` (working-tree copy, ` M`) |
| Start / End (Europe/Istanbul) | 2026-10-02 20:15:11 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        BLOCKED (see §7). Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      89 " M" · 576 "??" · 0 staged = 665 at start and at end (see ARTIFACTS.sha256).
                      No .git/index.lock. No git diff. No git write. No gh. No fetch.
Changed files:        this record folder only
Tests:                NOT RUN (forbidden). Test outcomes are read from the Q208 result file.
Commands run:         file reads, grep, sha256, and the read-only identity gate verify_module_id.py (exit 0)
Decisions:            none taken. No pack checkbox ticked.
Out-of-scope changes: none
```

## 1. Acceptance walk (`ACCEPTANCE-MATRIX.tsv`, 101 rows)

Every row has a verdict, a `path:line`, and the test behind it. Line numbers are looked up from the files by
`tools/q210_matrix.py` at generation time.

| Group | Pack lines | Rows | MET | NOT MET | INSUFFICIENT EVIDENCE |
|---|---|---:|---:|---:|---:|
| §16 acceptance criteria | 121–125 | 6 | 5 | 0 | 1 |
| §18 ready-for-dev checklist | 134–138 | 5 | 4 | 1 | 0 |
| §27 DEV prerequisites | 461–468 | 9 | 7 | 1 | 1 |
| §26 A01–A12 | 420–431 | 17 | 6 | 0 | 11 |
| §29 R01–R11 | 542–552 | 27 | 16 | 0 | 11 |
| §13 failure paths, §14, §15 | 108–117 | 7 | 4 | 1 | 2 |
| §17 test expectations | 129–130 | 10 | 7 | 1 | 2 |
| **Backend subtotal** | | **81** | **49** | **4** | **28** |
| §32.11 UI rows | 748–759 | 12 | 0 | 12 | 0 |
| §33 self-registration M-01…M-08 | 846–853 | 8 | 0 | 8 | 0 |
| **Total** | | **101** | **49** | **24** | **28** |

A criterion with several independent parts is split into rows `a`, `b`, `c`; that is why A01–A12 give 17 rows
and R01–R11 give 27.

Rule used for the verdict:

- **MET** — the code does it (path:line) **and** an in-tree test that passed in Q208 proves it; or the criterion is
  structural (a route list, a collection list, a record) and reading proves it.
- **INSUFFICIENT EVIDENCE** — the code path exists, and no test in the tree exercises it; or the only test is green
  for the wrong reason.
- **NOT MET** — the thing is absent.

The four backend **NOT MET** rows:

| Row | What is absent |
|---|---|
| RFD-18-4 (pack `:137`) | MOD-0183 dependency evidence that is *verified* (see §4) |
| PRE-27-6b (pack `:466`) | `Program.cs` single-writer release — Q209 is HELD |
| GW-15 (pack `:117`) | Gateway routes for `/returns**` — 0 in `ocelot.json` |
| TE-17-10 (pack `:130`) | The explicit no-shadow-balance architecture test. The property holds; the test does not exist |

The 20 UI and self-registration rows are NOT MET because no UI file and no provider exist. Both are outside
Q210's code scope and are not yet authorized (pack `:788`, `:859`).

## 2. Scope (`SCOPE-DELTA.tsv`)

- **Present and authorized: 46 files** = 36 source + 7 test + 3 probe. They are exactly the pack's §25 list, plus
  `ReturnQuantity.cs` (§29, pack `:522`), minus `ReturnOutboxWorker.cs` (excluded by §30, pack `:566`).
- **All 46 are byte-equal to the CT-accepted source.** 44 match `returns46.json`; `ReturnReferenceReader.cs` and
  `ReturnReferenceTests.cs` match the accepted final manifest `source-final-341.tsv` (the R01 product patch).
- **Present and not authorized: none.**
- **Required and absent:**
  - the Returns composition in `Program.cs` (shared seam, not an owned path);
  - `docs/records/audits/2026-09/mvp6-mod0186-phase15-close-01/owned-paths.txt` — `tests/returns/runtime_probe.py:12`
    reads it; the folder is not in the checkout (the pack says so at `:580`). The two owned probes cannot run as written;
  - the self-registration provider and its test (proposed paths; ship with the UI);
  - the 21 UI paths.

## 3. Boundary (`BOUNDARY-REFS.tsv`, 13 rows)

| Verdict | Rows |
|---|---|
| Permitted read-by-contract | B-05 — one HTTP `GET /api/shipment-bundle/shipments/{id}` (`ReturnReferenceReader.cs:72`); B-06, B-07 — contract literals |
| Permitted shared kernel | B-01 `EntityBase`, B-02 `CustomBaseController`, B-03 `Response<T>`, B-10 shared Mongo registration (test) |
| Permitted, opaque | B-08 Inventory reference as text; B-09 five module-owned collections only |
| **Boundary violation (type-level, low)** | **B-04** and its test-side twin B-11 |
| Nothing found | B-12 no other feature references Returns; B-13 no explicit reference into another feature |

**B-04.** `ReturnContextMiddleware.cs:105` writes `loggingContext.Scope = new(tenant, le, actor)`. The property is
declared as `ShipmentScope` — a MOD-0183 domain type (`Application/Common/RequestContext.cs:1`, `:5`). Returns code
therefore constructs a MOD-0183 type. It reads no Shipment data, changes no Shipment state and touches no intake
artifact. It is not a contract read, so it cannot be called "permitted read-by-contract". The cause is in the
shared `RequestContext`, not in a Returns design choice, and the file is byte-equal to the source CT accepted
before. CT to rule. No fix made.

No reference to Inventory, Warehouse, Supplier, Carrier, Load or Claim code, clients or collections exists in
`Features/Returns/`.

## 4. Pack line 137 — "MOD-0183 dependency has executable verified evidence"

**Satisfiable: yes. Satisfied today: no. Not ticked.**

| Part of the sentence | State | Evidence |
|---|---|---|
| "executable … evidence" | Present | 73/73 in Q205 and 74/74 in Q208, on this working tree (`mvp6-q205-testenv-01/MOD0183-EVIDENCE-STATEMENT.md`; `mvp6-q208-suite-baseline-01/NEW-BASELINE.tsv:2`) |
| What Returns actually needs from MOD-0183 | Present in the tree | `getShipment` emits `lifecycleCorrelationId` (`Application/Features/Shipments/ShipmentProjection.cs:31`); the reader consumes exactly that (`ReturnReferenceReader.cs:65-67`) |
| "verified" | **Absent** | Each run was one lane's own run. No independent VER has re-run MOD-0183 on this tree. CT has not accepted either run as dependency evidence. |

Why it is satisfiable: nothing technical is missing. Two acts are: an independent VER lane re-runs the MOD-0183
suite on the current tree, and CT records it as dependency evidence. Two limits to state when that happens:

- The run is on the working tree (uncommitted), not on a committed or stacked tree.
- The pack's own text already records an earlier *bounded* CT acceptance of MOD-0183 (pack `:182`, `:486`). That
  acceptance was for the isolated environment of September. It is not evidence for today's tree, and the box at
  `:137` was left unticked after it.

## 5. Test credibility — the three highest-risk criteria

| # | Criterion | Why highest risk | Test that proves it | Verdict |
|---|---|---|---|---|
| 1 | **Quantity invariant under concurrency** (pack `:123`, R02 `:543`): for source 10, 6+6 gives exactly one success; 4+6 gives 10 | Silent over-return of goods | `ReturnConcurrencyTests.cs:12` `Create_Races_RespectsExactCap`; `ReturnAtomicityTests.cs:21` `MultilineFailure_SecondLineOverCap_LeavesNoFirstLineDebit`. Code: `ReturnRepository.cs:197-198`, version CAS `:208-209` | **MET** (repository level, real Mongo; not via HTTP) |
| 2 | **Tenant / legal-entity isolation and RBAC** (A03 `:422`, R08 `:549`, `AGENTS.md:160`) | A miss is silent and leaks data | Data scope: `ReturnIsolationTests.cs:20` `Scope_AnotherTenantOrLE_CannotReadMutateOrReplay`, `:50` `Receipt_SameKeyDifferentLegalEntity_IsIndependent` → **MET**. Request context: `:26` middleware unit matrix → MET at unit level. Real token and per-target grant (`ReturnContextMiddleware.cs:94-95`): **no test** | **MET** for data scope · **INSUFFICIENT EVIDENCE** for JWT and per-target RBAC |
| 3 | **Five-collection atomic commit and unknown-commit handling** (A07 `:426`, R09/R10 `:550-551`, `:406-407`) | Partial state, or a lost or duplicated return | Rollback at each of 5 write stages: `ReturnAtomicityTests.cs:15` `PrecommitFault_EachActualWriteStage_RollsBackAllFive` → **MET**. Commit done, response lost → original receipt: `:23` `PostCommitResponseLoss_RecoversOriginalReceipt_NotZeroWrites` → **MET**. Repeated unknown commit / retry exhaustion: `:25` `UnknownCommit_Unresolved503_AllOrNoneThenSameKeyRecovery` is **green for the wrong reason** (F-Q214-3: armed `times: 5`, fires once) | **MET** for rollback and receipt recovery · **INSUFFICIENT EVIDENCE** for exhaustion |

The first two rows of item 3 use a probe that throws at a named write stage. They do not use the server fail
point and are not exposed to the Q214 mechanism.

## 6. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q210-1** | 🟢 Result | The 46 Returns files on disk are byte-equal to the CT-accepted bounded source. Nothing was added, dropped or altered by the Q202a write. | `SCOPE-DELTA.tsv` |
| **F-Q210-2** | 🔴 Blocker | The module is not reachable: no composition in `Program.cs`, no `Returns:ReferenceBaseUrl`, no gateway route, no registered permission key. | `REACHABILITY.md` §1, §4 |
| **F-Q210-3** | 🟠 High | The controller is **half-wired**, not unwired. `MapControllers()` maps its three routes in the service today; its dependencies are unregistered and its requests fall into the Shipment middleware branch. Run-time behaviour: insufficient evidence. | `Program.cs:68`, `:71`; `ReturnsController.cs:13-14`; `REACHABILITY.md` §3 |
| **F-Q210-4** | 🟠 High | No test in the tree exercises the HTTP surface, a real token, or the per-target grant. 78/78 is repository, validator, parser and middleware-unit evidence. The statement "Returns tests compose their own host" is not accurate. | `ReturnIsolationTests.cs:24`; `ReturnAtomicityTests.cs:55-60`; `REACHABILITY.md` §2 |
| **F-Q210-5** | 🟠 High | Retry exhaustion is untested (F-Q214-3 applied): rows A07c and R09c. | `ACCEPTANCE-MATRIX.tsv`; Q214 `BLAST-RADIUS.tsv:4` |
| **F-Q210-6** | 🟡 Medium | Seven product branches have code and no test: `SHIPMENT_NOT_RETURNABLE` (`ReturnRepository.cs:57`), `SHIPMENT_LINE_NOT_FOUND` (`:190`), `RETURN_UOM_MISMATCH` (`:196`), `DISPOSITION_REQUIRED` (`:94`), `SHIPMENT_NOT_FOUND` (`ReturnReferenceReader.cs:80`), source identity mismatch (`:64`), per-target grant 403 (`ReturnContextMiddleware.cs:95`). | rows A08, R01c, R01d, R02c, R05b, R06b |
| **F-Q210-7** | 🟡 Medium | Restart evidence cannot be reproduced: the owned probe reads a record that is not in the checkout, and in an ordinary run `RestartReceipt` seeds and reads in one process. | `tests/returns/runtime_probe.py:12`; `ReturnReplayTests.cs:16-22` |
| **F-Q210-8** | 🟡 Medium | Contract pin drift: the pack binds SHIPMENT-BUNDLE 3.0.0 `5dfe7c1b…`; the file on disk is 3.1.0 `6dc1dd48…` (` M`). The Returns operations, fields and enum on disk match the code by reading; parity with the bound bytes could not be checked. Returns annex and root annex match their pins. | pack `:584`; `docs/analysis/contracts/shipment-bundle.openapi.yaml:13` |
| **F-Q210-9** | 🟡 Medium | The pack contradicts itself in its checkboxes: `status: ready-for-dev` (`:9`) with "Owner approval changed status from draft" unticked (`:138`); §27 lines 464–468 unticked although §31 records them as closed for the isolated scope. Not corrected. | pack `:9`, `:138`, `:464-468`, `:586` |
| **F-Q210-10** | 🟡 Medium | Boundary: Returns middleware constructs the MOD-0183 type `ShipmentScope` through the shared `RequestContext`. No data access. | `BOUNDARY-REFS.tsv` B-04 |
| **F-Q210-11** | ⚪ Low | Observation: every transition overwrites the aggregate's `InventoryTransactionReferenceId` with the current command's value, including null. A reference given at `Received` is cleared from the aggregate by a later transition without one. The audit rows keep it. The annex does not say which is intended. | `ReturnRepository.cs:98`; annex `:52-53` |
| **F-Q210-12** | ⚪ Low | The pack expects an explicit no-shadow-balance architecture test; none exists. The property holds by reading. | pack `:130`; row TE-17-10 |
| **F-Q210-13** | ⚪ Info | The Q187 per-target-guard overlay is not in the tree (Q202b HELD; its VER is CT NOT ACCEPTED). This VER covers the pre-guard source. | `CT-QUEUE.tsv:303`; `REACHABILITY.md` §5 |

## 7. Overall verdict

**BLOCKED** for "module real". What is missing:

1. `Program.cs` composition for Returns and the Shipment-branch exclusion (Q209, single integration owner).
2. `Returns:ReferenceBaseUrl` configuration.
3. Gateway routes and the route-count guard.
4. Permission keys registered and seeded; token claims confirmed.
5. MOD-0183 dependency evidence that is independently verified and CT-accepted (pack `:137`).
6. HTTP / JWT / per-target RBAC evidence on this tree (A03, R05b, R08c). It can only exist after 1–4.
7. A real retry-exhaustion test (Q215).
8. Tests for the seven untested branches of F-Q210-6.
9. A restart run that can be reproduced (probe dependency, F-Q210-7).
10. A decision on the contract pin drift (F-Q210-8) and on B-04 (F-Q210-10).
11. Later, by design: UI, self-registration, navigation keys, event transport, inbound receiving.

What is **not** blocked, and can be stated plainly: the backend core in the tree is the same bytes CT accepted as
the bounded Returns work package; it compiles; its 78 tests are green; and by reading, it conforms to the Returns
annex on lifecycle, entitlement, replay, atomic commit and scope filtering. That earlier acceptance stands as it
was — bounded, isolated — and this VER neither widens nor withdraws it.

## 8. Known gaps of this VER

- Static only. No statement here is a run-time observation.
- The 46-file byte equality was checked against `returns46.json` and `source-final-341.tsv`; those manifests were
  taken as given (their own SHA256SUMS were not re-verified).
- The UI rows and M-rows were assessed only for presence.
- Auth service, gateway internals and the Shipment middleware were read only as far as the cited lines.

## 9. Refused / not done

- No build, no test, no service start. The identity gate script was the only program run; it only reads.
- No file under `.antigravity/`, `gateway/`, `frontend/`, `services/`, `scripts/` changed. No pack checkbox ticked
  — including `:137` and `:138`. No seam patched. No ledger row changed.

## 10. Files

`SOP-22.md` · `ACCEPTANCE-MATRIX.tsv` · `SCOPE-DELTA.tsv` · `BOUNDARY-REFS.tsv` · `REACHABILITY.md` ·
`ARTIFACTS.sha256` · `tools/q210_matrix.py` (generates the three TSVs; read-only on the repo)

Return to CT; CT decides.

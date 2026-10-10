# Owner decisions Q19 / Q15 / Q03b / Q03c / Q21 + CT disposition Q147 + UC-01 — 2026-09-27

Recorded by the Q150 LANE (Cowork LANE 1, Linux VM, repo via bridge; single ledger writer; @orchestrator + `/reconcile-records`,
record written in the documentation-writer role) at 2026-09-27T19:13+03:00 on CT instruction (CT writes no files).
The text of CT-1…CT-3 and OD-Q19…OD-Q21 is copied as given in the Q150 dispatch. Q150 preflight 19:12:55 +03:00.

## 1. Metadata (SOP §17.1)

| Field | Value |
|---|---|
| Work Package ID / Prompt | Q150 (as dispatched; no WP-ID suffix or prompt version stated) |
| Capability Block / Module | MVP6 process pilot — governance records; no module code |
| Agent Lane ID / Type | Cowork LANE 1 / DEV (records + ledgers only) |
| Target Agent / Entry Point | `@orchestrator` + `/reconcile-records`; record by documentation-writer |
| Risk Class | not stated in the dispatch |
| Target Branch / Expected Base HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched) |
| Dirty-worktree baseline | `git status --porcelain` only (no `git diff`, Q147 D-1): 29 tracked ` M` paths = the 19 known + the 10 UC-01 files (known, not drift); 6159 lines in all |
| Depends On | Q147 |
| Authority Sources | SOP `docs/guides/operations/control-tower-sop.md` v2.4 §17.1, §20, §25, §37 |
| Allowed Paths | this record; `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv`; `…/MILESTONE-EVENTS.tsv` |
| Protected Paths | everything else — the 10 UC-01 files, code, SOP and `.antigravity` untouched |
| Ledgers before (matched) | CT-QUEUE `7a3215819d6fa8f14fc7755dc8cedbb4b428ca70d97d347a10691fefa8f009fc` (175 rows) · MILESTONE-EVENTS `3b3057e13c7bac1c1dc360e7c9028e5a793e37f977959a98ed11124e899b78d6` (208 lines) |

## 2. Evidence (verified with `sha256sum -c` before writing)

| Item | SHA-256 | Result |
|---|---|---|
| `docs/records/decisions/2026-09/mvp6-q147-owner-decision-pack-01/SHA256SUMS` | `c77708d51b3c2eb194131f7bae99f1f713e23270a14d8358fd9b1f2e4d43e338` | 2/2 OK |
| `…/mvp6-q147-owner-decision-pack-01/DECISION-PACK.md` | `b311c4cecc00b872d1d484bb8d56ff1df467aa088c9ee1daf9b07a7c048ff45d` | listed |
| `…/mvp6-q147-owner-decision-pack-01/OPTIONS.tsv` | `81ae93ab5d8a59de0087186926b951a6eeac56419667e4e64516bc73c27366e4` | listed |
| `docs/roadmap/plans/mvp6-supplier-concurrence-close-prep-01/SHA256SUMS` (DC-01…DC-05 source) | `a27ba363434bda38cbcdc72a851baefb20731a23ba29cce3a1b51189f2e6b328` | 5/5 OK |
| `docs/roadmap/plans/mvp6-supplier-concurrence-close-prep-01/OWNER-DECISION-PACK.md` | `ed94295eb5b99139510e38267664ed5525143e09717c42332cd31e67867b36b7` | listed |

## 3. CT dispositions (verbatim)

- **CT-1** Q147: CT ACCEPTED (decision pack). D-1 incident: .git/index.lock 17:47:45 from a git diff killed by timeout. The owner removed it at 19:0x (Mac, confirmed). New rule: lanes use only git status --porcelain; no git diff under timeout.
- **CT-2** Uncontrolled change UC-01: 10 Platform test files in the working tree changed 17:45:57–17:49:36:
  MongoIntegrationHarness.cs, 4 × BusinessReferenceData*MongoTests, PlatformSchemaContractMongoTests, WorkflowTransitionGateMongoRepositoryTests, PlatformContainerValidationTests, 2 × RabbitMq*IntegrationTests.
  No WP, no record, no report; the owner started no work. Evidence points to a Cowork VM lane.
  Q131 was NOT run and stays READY. Do not change the files; the investigation is Q152.
- **CT-3** CT command error: "pgrep -fl git" matched ugrep processes, so the lock was not removed on the first attempt.

UC-01 file paths as measured by this lane (`git status --porcelain`, ` M`, not opened, not changed):
`services/Diten.Platform/tests/Diten.Platform.Application.Tests/Persistence/MongoIntegrationHarness.cs`;
`…/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceData{GskuCatalogLoad,MongoResidueSweeper,PublishOperation,TenantAssignment}MongoTests.cs`;
`…/Diten.Platform.Application.Tests/Schema/PlatformSchemaContractMongoTests.cs`; `…/Diten.Platform.Application.Tests/Workflow/WorkflowTransitionGateMongoRepositoryTests.cs`;
`services/Diten.Platform/tests/Diten.Platform.BackgroundJobs.Tests/PlatformContainerValidationTests.cs`;
`services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/{RabbitMqEventingIntegrationTests,TenantLifecycleRabbitMqIntegrationTests}.cs`.

## 4. Owner decisions (verbatim)

- **OD-Q19** A — DC-01…DC-05 approved as written (owner question tool, 27 Sep). For each item, write the decision-owner role exactly as stated in the pack; if it is not the project owner, write AWAITING-CT, do not guess.
- **OD-Q15** A — Final integration in a new registered checkout built from the declared base stack (Q143); refreshed exact-hash text for a second sign-off; execution after the module Mac builds.
- **OD-Q03b** B — Direction with the holds: X1/X2 + TestResults excluded; H1, D3 archives, overlay folders and runtime_probe.py held (until F02 is fixed); EXCLUSIONS refreshed before any commit. Q03a (no commit) stays in force.
- **OD-Q03c** A — Branch stays feature/mvp6-logistics until the end of MVP6; the §9 deviation is recorded once.
- **OD-Q21** A — Rev2 (process guide v1.0 §9) adopted as the MVP6 measurement policy.

### OD-Q19 per item (decision owner as stated in `OWNER-DECISION-PACK.md` `ed94295e…`)

| Item | Line | Decision owner (exact text) | Project owner? | Status |
|---|---|---|---|---|
| DC-01 durable domain/service ownership | :9 | Enterprise/domain owner, with CT and Supply Chain Execution concurrence. | No | **AWAITING-CT** |
| DC-02 MOD-0140 Supplier consumption and LegalEntity eligibility ownership | :29 | MOD-0140 Supplier owner and its eligibility owner; MOD-0147/0148 remain design consumers. | No | **AWAITING-CT** |
| DC-03 auth/security binding, current authorization and revocation fencing | :54 | Platform auth/security identity owner, with MOD-0148 and MOD-0140 concurrence at their boundary. | No | **AWAITING-CT** |
| DC-04 Metric Registry policy and immutable revision authority | :82 | Metric Registry owner; MOD-0147 is the design consumer. Risk owner concurs only on score bands under DC-05. | No | **AWAITING-CT** |
| DC-05 Risk taxonomy, score bands and source-resolution boundary | :102 | Risk Register owner with MOD-0147 lifecycle concurrence; MOD-0148 concurs on portal-source visibility; Metric owner concurs on the numeric band inputs. | No | **AWAITING-CT** |

The pack states at :5 that "another owner's approval is not concurrence". None of the five names the project owner as decision owner, so all five
stay AWAITING-CT here; CT decides whether the owner's A covers each named role.

## 5. Ledger rows appended (CT-QUEUE, append-only; since 2026-09-27; record = this file)

Q147 DONE (CT ACCEPTED) · Q19 DONE (OWNER DECIDED A) · Q15 DONE (OWNER DECIDED A) · Q03b DONE (OWNER DECIDED B) · Q03c DONE (OWNER DECIDED A) ·
Q21 DONE (OWNER DECIDED A) · Q150 DONE · Q151 READY (integration-agent; depends Q143) · Q152 READY (Mac Claude Code: devops-agent + read-only-auditor +
security-agent; no dependency) · Q136 SUPERSEDED (merged into Q152). Q131 stays READY (no row appended). MILESTONE-EVENTS: one hand-off line. No hours.

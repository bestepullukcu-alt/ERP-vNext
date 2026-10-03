# MOD-0184 VER-02 — FAIL: outage evidence records incorrect empty-body bytes

Date2026-09-17. Independent read-only verification of frozen DEV02 handoff. No CT acceptance, module advancement, E5/G5 or waiver.

## Scope and baseline

Repository `/Users/natig/Projects/ERP-vNext-recovery`, branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Strict repository-read-only; disposable hash-identical copy under this report directory used for build/tests/runtime. No source repairs, git mutations, or repository outputs. Fresh isolated Mongo replica set `mod0184ver` on27687 with test commands enabled; no operational Mongo data touched.

Read authority: AGENTS.md, read-only-auditor/workflow, DEV02/VER02v2.0, Carrier pack §§23/29/C01–C12, canonical1.1.0 and annex. Publication authority and consumer scope are already owner-approved; verification grants no new approval.

65-entry DEV inventory:64 non-self hashes PASS. DEV baseline14,327 files:14,326 protected unchanged, only authorized Program.cs difference. VER baseline14,393 content hashes and git status/staging/branch/HEAD/stashes match final. `git diff --check` exit0. See baseline.json, preservation-and-architecture.json and no-change.json.

## Finding VER-184-01 — High, evidence fidelity blocks handoff acceptance

`services/Diten.SupplyChainService/tests/carriers/restart_probe.py:36` records `base64.b64encode(r.data or b" ")`. A GET without a body has `r.data=None`; the recorder substitutes a space rather than zero bytes. The recorded request object is None while saved sent-body bytes are a space.

Independent interception immediately before urllib transport reproduced9 actual outage requests and9 saved records. Records0 and1 (GET200 and GET503) have actual base64 `""` versus saved `"IA=="`. All7 POST records match. Existing DEV evidence carries the same defect. Sources: independent-outage-capture.json, outage/outage.json, outage-capture-comparison.json. This is a recording defect, not a reproduced Carrier production functional failure. Production source changes are not required by this finding.

Separate evidence-only DEV rework must preserve actual zero-byte bodies, add a failing-before/passing-after capture regression for bodyless GET and present JSON bodies, and regenerate the outage/commit evidence with independent byte comparison. Include exact request counts and zero mismatches; do not simply relabel old saved evidence as correct. Reissue immutable handoff and inventory. Independent VER should rerun the repaired capture regression and affected live outage/commit probe, verify all changed hashes and preservation, and reassess architecture provenance. Broader tests need repeat only if production or other test behavior changes.

## Reproduced checks

| Check | Result |
|---|---|
| Fresh disposable-copy build | PASS0 warnings/errors |
| Service suite | PASS99/99,66 Shipment+33 Carrier |
| Carrier HTTP/schema/restart | PASS33/33; durable three-collection snapshots retained |
| Carrier primary runtime retained-byte check | PASS33, zero mismatches |
| Shipment unchanged real HTTP/restart/boundaries | PASS33, zero independent capture mismatches |
| Capture regression suite | PASS2/2; does not cover outage empty-body defect |
| Live persistence outage | PASS Q/C/S503; no listener on failed startup; same-key recovery201/replay201 |
| Real commitTransaction failCommand | PASS2 UnknownTransactionCommitResult failures recover201;3 failures exhaust503; same-key recovery exactly1 entity/receipt/audit |
| Standalone Mongo | PASS fail-closed before listener |
| Index creation failure | PASS in service suite |
| Architecture |14 PASS/4 FAIL; repository gate BLOCKED |
| Scope and preservation | PASS |

Commands/exits: results.json and runchecks.py. Logs: build.log, tests.log, carrier.log, capture.log, outage.log, shipment.log, capturetests.log, standalone.log, architecture.log. Actual uncertainty counters and persisted state: outage/commit-uncertainty.json. Build/run uses exact source; no test/source repair in disposable copy. Independent capture wrapper adds observation only.

## Source/acceptance review

C01–C10 functional checks reproduced, including exact three routes/wire, enum/schema, tenant×LE/RBAC, reserved-code collation, nine lifecycle pairs, historical replay before deletion, distinct-target barrier serial history, rollback and actual commit uncertainty. C11 operational logging bridge is Carrier-local after successful authorization; existing shared logging code remains unchanged. Error/current-header and original audit correlation separation verified by tests and runtime. C11 evidence-byte fidelity remains FAIL for outage records. C12 source/preservation PASS. Frozen create422 example check stays N/A, not runtime PASS.

Production scope: new Carrier feature folders in all five layers and approved minimal Program.cs registration/middleware/model-state composition. No frozen authority, other Shipment source, other services, Supplier/stock/ingress/events/gateway/UI changes by DEV. No concrete production functional defect reproduced during this review.

## Architecture provenance — no waiver

Fresh test independently reproduced four failures: the known Platform DB010 and HCM/Talent JWT guard failures plus DocsPathGuard. All17 distinct DocsPathGuard offending files were in the DEV baseline and retain their exact baseline hashes; no new Carrier runtime/evidence path appears as an offender. Thus the fourth failure is pre-existing to this DEV dispatch.

That does NOT mean all17 files predate the current CT task. `mod-0184-published-uptake/check_uptake.py` and `results.json` were created by CT during this current publication turn before the DEV baseline. The remaining15 offending files are earlier preparation/evidence. See preservation-and-architecture.json for exact per-file hashes/provenance and architecture.log for fresh guard output. Current-task publication outputs require CT disposition; no guard weakening, immutable history rewrite, or unapproved fourth-failure waiver is granted here.

## Final state

FAIL for evidence recording. Functional checks above remain valid observations, not CT acceptance. Repository gate remains BLOCKED. Original repository14,393 hashes, git branch/HEAD/status/staging/stashes unchanged. Owned HTTP/proxy/standalone processes stopped; verifier MongoPID7585 stopped and failpoints disabled by probe finally. No commit/push/stash. Return to CT for separate bounded evidence rework.

# MOD-0184 DEV-02 v2.0 — READY / owner-authorized dispatch

Supersedes DEV01 v1.0 execution; old file unchanged. Material contract version/update.

```text
@orchestrator /add-module
WP MVP6-MOD0184-DEV-02; prompt MVP6-MOD0184-P02 v2.0
Lane AL-MVP6-MOD0184-DEV02; type DEV; Profile B; HIGH; build lane MVP6-CARRIER.
Repository/worktree /Users/natig/Projects/ERP-vNext-recovery
Branch feature/mvp6-logistics; base HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c.
Dirty baseline: owner-published canonical YAML/annex, approved Carrier pack and prior untracked
CT/preparation/evidence/plans. Freeze exact hashes at dispatch; preserve all outside allowlist.
Authority: AGENTS.md; Supply Chain domain; MOD0184 Carrier pack ready-for-dev §§21–29;
release-and-dev-go-2026-09-17.md in docs/records/audits/2026-09; canonical SHIPMENT-BUNDLE1.1.0
and carrier-semantics-v1.1.0.md; CT SOP and applicable Antigravity rules and add-module workflow.
Published YAML sha ba9d85f086dd2bfc150c1818843fa22c5b00b0dba1948a57a3672e2529d9880f;
annex sha87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee.
Domain supply-chain-execution; service Diten.SupplyChainService port5061; shell none;
golden_reference none; form_field_count0; no DataTable. Phase1.5 approved by owner.
Dependencies SATISFIED: bounded Shipment acceptance, published Carrier contract/test uptake.
Parallel-safe none for writers. Single writer owns Program.cs; specialists sequential inside lane.
Chain: business analysis → data/backend → security → testing → documentation; independent VER later.

NE: Implement only queryCarriers/createCarrier/changeCarrierStatus and pack C01–C12.
NEDEN: Owner-authorized next MVP6 module; exact contract and runtime design gates now satisfied.
NASIL: Exact pack §23 paths. Five layers, separate CQRS/validators/handlers, internal Response<T>,
CustomBaseController with unwrapped Carrier JSON. Reuse existing EntityBase and IMongoDatabase;
Carrier transactional repository handles entity+success receipt+audit atomically (L3).
Permissions supplychain.carriers.read/create/status.change. Validate signed tenant/LE/actor and
headers in annex order, nil correlation accepted, key codepoint length1..128 without trimming,
exact code/name/modes strings, same-root and different-root replay per Carrier annex.
Preserve original receipt status/result/root/actor; current response correlation, historical replay
before current target lifecycle/deletion; no TTL. Serializable valid lifecycle and unique exact code
including retired/deleted. Safe bounded transaction retry/unknown commit handling, no false rollback.
Allowed: new Carrier feature folders and tests/probes exactly pack §23; evidence
 docs/records/audits/2026-09/mod-0184-dev-01/; minimal Program.cs composition exception only.
Program.cs isolates Carrier middleware/model errors from Shipment, registers Carrier context/store;
all other existing source/project files protected. No new project refs assumed; report real gap.
YAPMA: No frozen contract/annex/pack/governance edits, no Supplier master, Inventory write, ingress,
Carrier events, new public CRUD routes, UI/gateway/catalog, other services, commit/push/stash.
DOĞRULA: DCP002, build, Carrier tests plus unchanged Shipment suite/probes, fresh architecture
baseline15/3 with no new failures. Real isolated Mongo replica-set, fixed DB names with unique tenant
fixtures. Exact HTTP capture plus persistence/audit/replay counts; lifecycle9 pairs, 2x2 scope, RBAC,
header precedence, replay/conflicts, barriers, faults after each transactional write, unknown commit,
process restart/recovery, cross-route regression. No operation data/secret leakage.
Published create422 examples N/A; do not invent a create rule or call it runtime tested.
E2/E3/E4 required. E5/gateway/full module acceptance not claimed. Record process/binary freshness.
No routine questions: use approved contract/defaults, ASSUMPTION when needed. Stop affected work
for actual protected-path/contract/security conflict or unexpected file mutation, not routine choices.
Output SOP§22 report, exact inventory/non-self hashes, preservation hashes, commands/exits, AC map,
remaining gaps and independent VER handoff. Finish implementation and meaningful tests, not plan only.
```

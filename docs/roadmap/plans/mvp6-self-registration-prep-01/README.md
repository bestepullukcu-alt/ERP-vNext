# MVP6 self-registration prep — Supply Chain service (CT queue Q40)

🤖 Applying knowledge of @module-pack-author (with @backend-architect, @l10n-agent for review).

**PREPARED PROPOSAL — NOT APPROVED.** Documents only. It answers compliance finding AG-01
(`docs/records/audits/2026-09/mvp6-antigravity-compliance-check-2026-09-26.md`): `services/Diten.SupplyChainService` has no `ModuleManifestProvider`.
Repo `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Start 2026-09-26T08:55:57+03:00.

## Result in one table

| Item | Finding |
|---|---|
| Structure | Capability-level foundation (DCP-009 follow-up, single integration owner) + per-module manifest sections in packs 0183–0187; S&OP/Capacity excluded (no UI) |
| Modules | Shipment (3 pages, 4 actions) · Carrier (1 page, 2 actions) · Loads (1 page, 1 action; UI not approved) · Returns (1 page, 8 actions) · Claims (1 page, 7 actions) |
| Scope | All routes `/SupplyChain/...` → Tenant scope; none under `/Platform/` |
| Nav keys | 11 keys × 7 languages; 0/7 present today; 3 have unapproved draft values (Carrier candidate) |
| Prior art | Unapproved Carrier candidate `MODULE-REGISTRATION.patch` / `NAVIGATION-L10N.patch` — identity choices kept |
| Effort | 38 / 70 / 127 h (D2=A) |
| Decisions | D1–D5 in OWNER-DECISION-TEXT.md |

## Honest gaps

1. **UI source is not in the common checkout.** Shipment UI routes come from the archived A12 successor source (`7b6a0d1a…`). The Carrier v2 UI is known only
   from VER records and scope files (its source is not archived in the repo). Loads UI is HELD and unbuilt; Returns/Claims UIs are approved but unbuilt.
2. **Permission classes:** Shipment, Carrier and Loads constants are in the common checkout. Returns and Claims classes exist only in the accepted isolated
   source. Returns target keys are mapping results (`ForTarget`), not constants (D5).
3. **Single-key actions:** the manifest cannot express Returns' `.transition` + target key, or the approved `supplychain.shipments.read` prerequisite for
   Returns/Claims create and transition. The declared key is the target key; the conjunction stays enforced by the backend and UI only.
4. **Platform auth:** the credential gate is hard-coded to two MDM ModuleCodes; per-service credentials for SupplyChain need a Platform change (D2=B).
5. **New domain:** no Supply Chain domain exists in the Platform catalog; self-registration will create it under the normalized key, so
   `Nav.Domain.SUPPLYCHAINEXECUTION` becomes mandatory with the first provider.
6. **Guard coupling:** `NavManifestL10nGuardTests` parses every `*ManifestProvider.cs` in `services/`, so a provider merged without its 7-language keys fails `dotnet test`.
7. **Reference inconsistency (noted, not changed):** `GoldenSlimManifestProvider` uses ModuleCode `GOLDENSLIM` (uppercase) although the standard asks for a lowercase slug;
   `add-module.md` still names `NavL10nContractTests` while the standard names `NavManifestL10nGuardTests`.
8. **Runtime tests** R-02…R-04 need a native executor and the integrated target.

## Files

| File | Purpose |
|---|---|
| `DECISION-STRUCTURE.md` | Classification, where each part belongs, sequencing, prior art, decisions |
| `MANIFESTS.md` | Per-module ModuleCode, pages, actions, keys, scope, gaps |
| `NAV-L10N-KEYS.tsv` | The 11 `Nav.*` keys, 7 languages each |
| `TEST-PLAN.md` | Foundation, per-module both-direction, frontend guards, reconcile-state |
| `OWNED-PATHS.md` | Owned, protected and single-writer paths with today's preimage hashes |
| `EFFORT.md` | O/M/P by delivery |
| `OWNER-DECISION-TEXT.md` | Exact owner text — NOT APPROVED |
| `SHA256SUMS` | Checksums (paths relative to this directory) |

Inputs were read, not changed. The Shipment archive and one accepted Claims archive were extracted into a scratch area outside the repository to read the
controller and the permission classes. No code, pack, contract, resx, `.antigravity`, gateway or record was edited; no commit or push.

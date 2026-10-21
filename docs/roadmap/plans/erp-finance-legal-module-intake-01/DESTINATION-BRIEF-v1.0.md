# Treasury transfer — destination brief for the producer CT (v1.0)

    version   : 1.0
    created   : 2026-10-21
    status    : MEASUREMENT / not a decision. Written by the Recovery (destination) CT for the ERP-vNext Treasury
                (producer) CT, which may not read the destination repository itself.
    destination: bestepullukcu-alt/ERP-vNext ("Recovery"), origin/main 9d44e8a1f (fetched 2026-10-21, unchanged)
    method    : read-only on origin/main; no build, no run. Every hash below is sha256 of the file at 9d44e8a1f
                unless marked "untracked".
    owner     : Natig (management). Packet acceptance and every later gate stay with the owners named in TP-001.

## 1 · What the producer must follow

| what | path in destination | sha256 |
|---|---|---|
| Packet rules TP-001 v1.5 (**PROPOSAL / UNAPPROVED**, untracked) | `docs/roadmap/plans/erp-finance-legal-module-intake-01/MODULE-TRANSFER-PACKET-RULES-v1.5.md` | `3a60135eff9a84dc4fdef39c42b113c8c99f1c4c8df326aca4a817b30458ea65` |
| Treasury migration sequence v1.0 (PROPOSAL, untracked) | `…/TREASURY-MIGRATION-SEQUENCE-v1.0.md` | `b671f67d1e83e5aefc981fb4408cd5af66d1fbd69106a6b27d429471c4a60881` |

Until TP-001 is approved, use its **packet shape (§4 A–I), the producer/destination split (§4a) and the refusal
conditions (§5)** as the target format; the producer gets a copy of the file from the owner. Key rules: **T25 — a
packet is a document, never a code drop** (UI as a specification, not view files); T5 — four states, never one
word: DECLARED · IMPLEMENTED · WIRED · RUNNING; T8 — `path:line@commit` or move the claim to the limits section;
T21 — source readiness labels are never destination readiness; T27 — BLOCKED is a valid outcome.

## 2 · Destination identity facts

- **Blueprint here is master 8.8** — `docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.8.xlsx`,
  sha256 `edbcb725848f7fe5ee3f143eee3285fd798d9204b4a013c27f704d69fc1a5939` (sheet `Blueprint_Data`). The source uses 8.1;
  **every packet states the canonical name as 8.8 has it** (T1). The names below were read from 8.8 and match the source's.
- Registry: `execution/registries/module-id-registry.md`, sha256 `dcf7d2be066fe7fe4f47416105e39b6f637ae5b45764c073efd4f39886793a2d`.

## 3 · Each module in the destination today (T10)

| module | canonical name (8.8) | wave | registry row here | pack here | code here | Blueprint dependency gate (8.8, verbatim) |
|---|---|---|---|---|---|---|
| MOD-0289 | Bank Master & Bank Account Registry | W-3 | **none** | none | none | Data Contract Registry; RBAC / ABAC Authorization; Audit Trail Service; Workflow Designer (Approvals/SLAs/Escalations); **Payments & Approvals; Corporate Secretarial / Entity Management; Supplier Onboarding (KYC/Sanctions/Docs); Banks/SWIFT/Payments Gateway [External Provider]** |
| MOD-0220 | Corporate Secretarial / Entity Management | W-3 | `ready-for-dev`, master-data-management | `execution/domains/master-data-management/module-packs/MOD-0220-legal-entity-foundation.md` (`status: ready-for-dev`, `service: Diten.MdmService`), sha256 `a8e29dc89128e05430936c8e207d7718f530c9b398019477b53f7b6fb624dc40` | **65 files** (`Diten.MdmService`, `Diten.Platform`) | Records Management |
| MOD-0119 | Accounts Payable (AP) | W-3 | none | none | none | Workflow Designer; Data Contract Registry; Audit Trail Service |
| MOD-0140 | Supplier Onboarding (KYC/Sanctions/Docs) | W-1 | `ready-for-dev`, procurement | `…/procurement/module-packs/MOD-0140-supplier-onboarding.md` | **50 files** (`Diten.ProcurementService`) | SSO / MFA; Workflow Designer (Approvals/SLAs/Escalations); Documentation & Evidence Management; Policy & Control Library; Sanctions/KYC Screening Provider [External Provider] |
| MOD-0120 | Accounts Receivable (AR) | W-3 | none | none | none | Data Contract Registry; Billing integration surface |
| MOD-0149 | Customer 360 / Account Hierarchy | W-1 | `reserved / planned`, commercial-suite | `MOD-0149-customer-360-account-hierarchy.md`, `MOD-0149-PREREQ-diten-crm-service-scaffold.md` | **53 files** (`Diten.CrmService`, `Diten.AuthService`, `Diten.Platform`) | SoR & Ownership Registry; Canonical ID; Data Contract Registry (degraded until W-2) |
| MOD-0126 | Payments & Approvals | W-3 | none | none | none | Workflow Designer (Approvals/SLAs/Escalations); Audit Trail Service; Banks/SWIFT/Payments Gateway [External Provider] |
| MOD-0217 | Contract Lifecycle Management (CLM) | W-3 | none | none | none | Document Management; Workflow Designer; Audit Trail Service |
| MOD-0124 | Cash Positioning | W-3 | none | none | none | Data Contract Registry; Integration Monitoring & Reconciliation |
| MOD-0269 | Banks/SWIFT/Payments Gateway [External Provider] | W-1 | none | none | none | — (External Systems Register) |
| MOD-0057 | Semantic Tagging & Taxonomy Management | W-4 | none | none | none | Data Dictionary/Glossary; Enterprise Search |
| MOD-0125 | Cash Forecasting | W-4 | none | none | none | Data Warehouse/Lakehouse; Metric & Semantic Registry |
| MOD-0146 | Payment Run Support | W-3 | none | none | 1 file (`Diten.ProcurementService`) | Payments & Approvals; AP |

"Code here" = files under `services/`, `frontend/`, `gateway/` mentioning the ID; it is a presence signal, not a
readiness measure.

**Consequences for the producer's batch order** (producer proposal 2026-10-21: 1 = MOD-0289 + MOD-0220, then
0119+0140, 0120+0149, 0126+0217, 0124+0269, 0057):

1. **MOD-0289's gate depends on later batches** — Payments & Approvals (batch 4), Supplier Onboarding (batch 2) and
   the Banks/SWIFT gateway (batch 5, parked). A MOD-0289 packet can be prepared first, but the packet must list
   these gates as **unmet in the destination** (T12), and destination integration of MOD-0289 cannot start before
   they are decided (built, stubbed by an approved contract, or waived by the owner).
2. **MOD-0220, MOD-0140 and MOD-0149 already exist here.** Their packets are **merge / compatibility** packets, not
   intake: MOD-0220 is a confirmed complementary fork (`docs/records/audits/2026-10/mod-0220-dual-implementation-01/REPORT.md`,
   untracked, sha256 `1612d765e55075709cc62e3b79ae528b60f1e86c1d2c8b430546b78060b68974`); MOD-0140 two competing systems of record (`…/mod-0140-dual-implementation-01/`).
   Describe the source half against the destination's existing pack and contract — do not propose a second copy.
3. Every other module has **zero** presence here: a clean intake, but there is **no finance/treasury domain and no
   treasury service** in the destination. Placement (domain + service + port) is an owner decision that must precede
   any destination pack (`MOD-0126-PLACEMENT-DECISION-PACK.md` in the same folder; its proposed port 5066 is now
   taken — see §5).

## 4 · Destination standards the packet must describe against (T4, T18)

| topic | destination file | sha256 |
|---|---|---|
| Project contract (authority order, core rules) | `AGENTS.md` | `f54732b2f1336fdfded4d8716b49a7eed5bae9feabf98dad08d354b5592b0d11` |
| Tenant + LE isolation, soft delete | `.antigravity/rules/multi-tenancy.md` | `4ba429f6729e4d6a073cc992044c4672675334e9021bb6708243fa6c1fe530f5` |
| JWT | `.antigravity/rules/security-jwt.md` | `1b99d7630fe883ff7babf1c6bdb9372215400d8c10580a12532698355a1996c5` |
| Permission keys (`[HasPermission]`) | `.antigravity/rules/permission-key-standard.md` | `b428b244cc12874777bffcb1af663863f75ebea8fd9a9d73c94e6959048d5eae` |
| Audit trail AUD-001 (MOD-0021) | `.antigravity/rules/audit-trail-standard.md` | `72e3f6d4b6039eff35f634704ce97b2eb5cd34ad60ac24526b771960def41622` |
| Record codes RCS-001 | `.antigravity/rules/record-coding-standard.md` | `e845217c6eb56636f6dc6cebe41f24b57d8ca417505e20950afe645a3455ee77` |
| Localization (tenant module: en, tr, fr, es, zh, ar, ru) | `.antigravity/rules/localization-standard.md` | `fc0f624267b78fe8cab6647c89f46873151ee96e53d4d231fde3ff7d11a0da5d` |
| Unauthorized surface UAS-001 | `.antigravity/rules/unauthorized-surface-standard.md` | `16f9fab927f45420c934b12d28f3a4673661cbdc30da6a5c3b22b924c3657331` |
| Module pack shape (incl. CT-APR-01/02) | `.antigravity/rules/module-pack-standard.md` | `cf6e9aad75acab3e79b4221baa5cbcf5c65472c53e05f10fcc221041c5381f5b` |
| Routes / gateway | `.antigravity/rules/routes.md` | `5aff3926b57e4c54b493863191243c7507b630316fd53a24bb8ab8062418f7f3` |
| Cross-module consent XMC-001 | `.antigravity/rules/cross-module-change-consent.md` | `60374c6342ae02e59544eca03ed2a8fbb9d7f6c623d0e4b3d72c28b8a042e4b2` |
| Entity base | `.antigravity/rules/entity-base-template.md` | `703f5d81331b407cf635ea4434b7da341f756ce2def03f9a5b2fc688b5107919` |
| Response envelope | `.antigravity/rules/response-envelope.md` | `f060f30a1bbc4659e2e1b6e50882a2305c913252db4cc81e14ee4696d09dc9d3` |
| Module self-registration | `.antigravity/rules/module-self-registration-standard.md` | `ce4ee7dea083c7e955f9abb21146f509f19b1340b1e6cdc26f7bf7e909185863` |
| Ports | `.antigravity/rules/ports.md` | `3b0948ae0f2bc0a8b55bb8c3ed7c89f23452a1def226f0fa09b8cd3352b47877` |

The destination's LE model has **three separate layers** (do not merge them in the packet): the user's LE rights
(`Diten.Platform.Application/Authorization/OrgDataScopeResolver.cs`), the LE's validity — belongs to the tenant,
active (`Diten.SupplyChainService.Infrastructure/Common/MdmLegalEntityScopeValidator.cs`), and the data filter in the
repository layer. Gates present here: MOD-0021 Audit (`MOD-0021-general-audit-trail.md` + FU01/FU02) and MOD-0023
Workflow (`MOD-0023-workflow-config-approval-templates.md`).

## 5 · Ports and services — do not carry source values

Assigned in the destination's `ports.md`: 5000 gateway · 5001 web · 5056 Auth · 5057 Platform · **5058 DevEnablement**
· **5059 MDM** · 5060 HCM · 5063 HumanCapital · 5064 TalentEcosystem; SupplyChain moved to **5066** (DCP-009, #173).
Source values collide: source Treasury **5058** = destination DevEnablement; source MDM 5050 ≠ destination MDM 5059.
The packet names **no destination port or service path**; it states the source values as source facts only.

## 6 · Delivery to the destination CT

- One packet per module, versioned filename, its own `manifest.json` (sha256 + size of every document, no
  self-reference); frozen source bytes behind every cited file (an export or a commit — dirty working-tree bytes are not
  evidence the destination can re-check).
- Put the packet in the producer repository and give the owner: the folder path, the manifest sha256, the source
  branch + HEAD, and whether the bytes are committed or exported. The destination CT verifies every hash before
  reading further and writes the **destination supplement** (§4a) itself.
- No secrets (T20). No destination paths invented. No destination readiness claimed.

## 7 · Not measured

No runtime check in the destination; no measurement of whether MOD-0220's destination half already covers what the
source's MOD-0289 needs from LE; port availability for a future treasury service; the producer's batch packets
(none received yet).

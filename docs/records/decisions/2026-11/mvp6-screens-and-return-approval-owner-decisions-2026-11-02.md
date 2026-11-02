# MVP-6 screens and return approval — four owner decisions (owner decision, 2026-11-02; K1 corrected 2026-11-03)

- owner: **Natig** (`gmg-10`, K1, management) — decided in chat 2026-11-02, K1's customer source corrected 2026-11-03, recorded by the MVP-6 Control Tower
- source: the MVP-1 live tour of the shipment and return screens (expected results r4, kept outside the repository in the DL-1 lane: `MVP1-TOUR-EXPECTED-RESULTS-r4-2026-11-02.md`), which found GUID and free-text fields, missing pickers and approval gaps
- applied in (drafts, not approved): MOD-0186 revision r9 (`execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md`, section "Screen slice B and return approval") and MOD-0183 revision r8 (`execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md`, section "Screen slice B")

| # | Question | Decision |
|---|---|---|
| K1 | Who are the sender and the recipient of a shipment? | **Sender:** the company of the session. **Recipient:** selected from a **MOD-0149 CRM account** (Natig 2026-11-03; this replaces the 2026-11-02 wording "MDM customer record", see the correction below). `shipToReference` carries the account id; the contract does not change. The picker needs `crm.account.read`. v1 uses the account's single address. **Intercompany shipment is out of v1** and needs a separate decision. |
| K2 | Where does return approval live? | `Requested → Authorized` is approved through **Platform Workflow and the Task Center**. The quality decision on received goods **stays in the MOD-0175 quarantine flow**, and the return's disposition is bound to that decision: the same goods never get two quality approvals. **Shipment dispatch stays permission-based** and needs no workflow. |
| K3 | Q-09: does a correcting REVERSAL restore the reservation? | The correcting REVERSAL of a **late dispatch record** sends `restoreReservation: true`; it does **not** send it when the shipment has a `Dispatched` attempt. Effective only with the approval of MOD-0173-FU03 (see below). |
| K4 | How are the screen fixes cut? | Two slices: **A** (PR `bestepullukcu-alt/ERP-vNext#355`, short fixes) and **B** (everything that needs a pack revision). No code of slice B is written before these revisions are approved. |

## Correction of K1 (2026-11-03)

The first wording named an "MDM customer record". Measured on `main` `49012bd7b`: `services/Diten.MdmService/src` has no file containing `customer`; `verify_module_id.py --discover "customer"` names MOD-0149 Customer 360 / Account Hierarchy as owner of `accounts/customers`; `docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0149-v1.0.md` item 1 records the same owner. The MVP-6 CT raised the conflict and Natig decided for MOD-0149 on 2026-11-03.

## Open items decided (Natig, 2026-11-03)

| Item | Decision |
|---|---|
| OD-186-2 | Return reasons are a closed, module-owned code list. Old returns keep their text as written. |
| OD-186-3 | Four-eyes applies: the approver may not be the person who opened the return. If the approver cannot be read, the outcome is not applied. |
| OD-183-2, OD-186-5 | A read of the draft PRODUCT-MASTER is accepted for display and selection only. Field names are bound at Phase 1.5. |
| OD-183-1 | Old shipments show the current name. |
| OD-183-3 | A hand-made shipment uses `MOD-0183` / `MANUAL`. |

## Carried as open items, not decided here

- **OD-186-4.** Depth of the location picker: to be measured.
- **`accountType` filter.** Proposal: no filter in v1.

- **MOD-0149 contract.** `docs/analysis/contracts` holds no CRM contract file; the account read used by the picker is measured from `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/AccountController.cs`.
- **TRM-09.** The MOD-0149 registry row says `reserved / planned` while its pack says `ready-for-dev` (`docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv`). Its owner resolves it.
- **K3 and MOD-0173-FU03.** The MVP-6 CT reports that FU03 r4 (`d9c378d4e`, not approved) left Q-09 to MVP-6, that Natig chose option A, and that r3's wording is out of date. That commit was not readable in this checkout, so its text is **not measured** here. FU03 is not on `main`.
- **Binding of disposition to the MOD-0175 decision (K2).** Written together with Beste; left open in MOD-0186.
- **SHP / RMA record codes.** A separate follow-up after MOD-0040-FU02; no identity is minted here.

## Effect

- The two revisions are drafts. Each needs an independent review and an approval row (CT-APR-02, CT-APR-01) before any code.
- The contract changes they name are listed in each pack; no contract file is changed by this record.

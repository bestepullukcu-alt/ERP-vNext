# MVP-6 screens and return approval — four owner decisions (owner decision, 2026-11-02; K1 corrected 2026-11-03)

- owner: **Natig** (`gmg-10`, K1, management) — decided in chat 2026-11-02, K1's customer source corrected 2026-11-03, recorded by the MVP-6 Control Tower
- source: the MVP-1 live tour of the shipment and return screens (expected results r4, kept outside the repository in the DL-1 lane: `MVP1-TOUR-EXPECTED-RESULTS-r4-2026-11-02.md`), which found GUID and free-text fields, missing pickers and approval gaps
- applied in (drafts, not approved): MOD-0186 revision r9 (`execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md`, section "Screen slice B and return approval") and MOD-0183 revision r8 (`execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md`, section "Screen slice B")
  - *Superseded by* the section "2026-11-03 — MVP-1 CT answer on customer identity (MOD-0177)" below: the current drafts are MOD-0183 r9 and MOD-0186 r10, with the SHIPMENT-BUNDLE 3.5.0 proposal and patch. The line above is kept as written.

| # | Question | Decision |
|---|---|---|
| K1 | Who are the sender and the recipient of a shipment? | **Sender:** the company of the session. **Recipient:** selected from a **MOD-0149 CRM account** (Natig 2026-11-03; this replaces the 2026-11-02 wording "MDM customer record", see the correction below). `shipToReference` carries the account id; the contract does not change. The picker needs `crm.account.read`. v1 uses the account's single address. **Intercompany shipment is out of v1** and needs a separate decision. |
| K1 (note, 2026-11-03) | — | *Superseded in part:* the sentence "`shipToReference` carries the account id; the contract does not change" is replaced by the section "2026-11-03 — MVP-1 CT answer on customer identity (MOD-0177)" below (separate `customerRef`, SHIPMENT-BUNDLE 3.5.0). The K1 text above is kept as written. |
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

## 2026-11-03 — MVP-1 CT answer on customer identity (MOD-0177)

- given by: the **MVP-1 Control Tower**, as owner of MOD-0177, on 2026-11-03, answering the DL-3 precondition of MOD-0183 r8
  (section "Screen slice B", effect on MOD-0177 recall)
- accepted by: **Natig (K1)**, 2026-11-03
- applied in (drafts, not approved): MOD-0183 r9, MOD-0186 r10, and the SHIPMENT-BUNDLE 3.5.0 proposal
  (`docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.5.0-PROPOSAL.md` and `SHIPMENT-BUNDLE-3.5.0.patch`)

This refines K1's sentence "`shipToReference` carries the account id; the contract does not change". Sender, recipient source
(MOD-0149), `crm.account.read`, the single address and intercompany out of v1 stay as decided.

1. **Separate field.** Customer identity is not written into `shipToReference`. SHIPMENT-BUNDLE 3.5.0 adds, additive only, a
   separate `customerRef` field, with the same name on shipments and returns. Value: MOD-0149 `Account.Id`, Guid "D" format,
   lower case; comparison is case-insensitive. `shipToReference` stays the delivery destination. Reason: no two meanings in one
   field, and no mix-up with the `shipToReference` values already stored.
2. **Screen.** No GUID is shown to the user. On the shipment and return forms the customer is chosen from a list by account
   name and address; the id goes to `customerRef` behind the scenes. List and details screens show customer name and code,
   never the id.
3. **Picker source.** CRM's list and address endpoints have no contract today. Until the CRM owner decides, the
   "contractless, measured" use that CRM described is acceptable: to the browser only name, code and the four address fields,
   and nothing of it in logs. When the contracted endpoint opens, the screens switch to it (open item, owner: CRM / MOD-0149
   owner).
4. **MOD-0177 rule.** If `customerRef` is set it is the customer key; if it is empty, today's order stays (`shipToReference`,
   then `"DOC:" + sourceDocumentId`). Name and code are resolved with CRM-ACCOUNT-LOOKUP 1.0.0 U-1 (`lookup-validation`;
   today a draft: `docs/analysis/contracts/drafts/crm-account-lookup-validation-v1.0-proposal.md`). MOD-0177 is added to that
   contract's consumer table by MVP-1 / CRM, not by MVP-6. A deleted account keeps its id, and its name shows "not found".
5. **SB-19 (same customer on two rows).** No automatic merge. Old-key rows (free text, `warehouse-outbound:…:ship-to`) are not
   linked to the new id by a guessed match; the screen shows them separately, marked "old record". This is an accepted limit.
6. **Follow-up and precondition.** MVP-1 opens a small MOD-0177 FU once it sees the MVP-6 3.5.0 draft. This text replaces the
   written confirmation that MOD-0183 r8 made a DL-3 precondition; that precondition is closed.

Natig (K1) accepted 2026-11-03.

### Same day — Natig (K1) decided the open questions of the drafts

On 2026-11-03, after reading MOD-0183 r9, MOD-0186 r10 and the 3.5.0 draft, **Natig (K1) decided**:

1. **OD-183-4.** The create adapter writes the chosen CRM account's address as one text into `shipToReference`; the field keeps
   its meaning, the delivery destination.
2. **Sequencing.** The whole of SHIPMENT-BUNDLE 3.5.0 (search, summary fields, `customerRef`) lands with MOD-0183 DL-3; MOD-0183
   DL-4 starts after DL-3.
3. **`getReturn` joins 3.5.0 — `getReturn` only; DL-5 surface kept out.** MOD-0186 "Contract changes needed" item 1
   (`getReturn` with `ReturnDetail`: `reasonCode`, `createdAt`, lines; no authorization or workflow field) moves into 3.5.0,
   additive only, served from MOD-0186 DL-4. Items 2–4 (`submitReturnAuthorization`, `authorizationState`,
   `lastAuthorizationOutcome`, `workflowInstanceId`, the five Returns workflow codes, the `transitionReturn` text) are DL-5,
   stay fully outside this approval and go in a later contract version opened with DL-5 (OD-186-1, OD-186-6 pending).
   (A first application of this decision on the drafts had moved items 1–4; it was corrected the same day to item 1 only.)
4. **MOD-0186 owned files.** MOD-0186's owned-files list names what DL-3 and DL-4 now change (`CreateReturnHandler`,
   `ReturnReferenceReader`, the return model, the Returns views and scripts).

Sent to the **MVP-1 CT** as open items, with the drafts' current behaviour as the default until they answer: whether the return
form needs its own customer choice (default: the customer is the chosen shipment's, copied onto the return; MOD-0186 OD-186-7),
and whether the customer picker list itself must show the address (default: name and code in the list, the address after the
choice; MOD-0183 OD-183-5, MOD-0186 OD-186-8).

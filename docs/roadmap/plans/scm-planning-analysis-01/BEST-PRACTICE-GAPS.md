# Best-practice comparison and gaps

🤖 Applying knowledge of @business-analyst + @product-manager. Practices are described as generally used methods (ASCM/APICS body of knowledge, S&OP/IBP, GS1, EU GMP/Falsified Medicines Directive); no document is quoted. Rule IDs refer to RULES.tsv. **Options only — nothing is decided here.**

## Summary

| # | Topic | Current rule | Gap | Recommended option |
|---|---|---|---|---|
| G1 | Safety stock | SOP-37/38: average-daily sell-out × (max − average) lead time | No demand or lead-time variability, no service level | Statistical safety stock; keep current formula as transitional |
| G2 | Forecasting & accuracy | SOP-24 manual sell-out forecast; SOP-15 plan split | No accuracy/bias measure, no statistical baseline | Baseline + consensus overlay; track MAPE/WMAPE and bias |
| G3 | Time fences / forecast consumption | SOP-27/34 roll-over of unshipped forecast | No frozen/slushy/liquid zones; roll-over can inflate demand | Planning time fences; forecast consumption by actuals |
| G4 | Lot sizing | AO-01 round up to minimum batch | Only a minimum; no multiple, maximum or economic lot | Lot-sizing policy per SKU (min, multiple, max, fixed period) |
| G5 | Capacity | none in the files (MOD-0192 = scenario/bottleneck backend only) | Order need ignores factory capacity | Rough-cut capacity check of order/production needs |
| G6 | S&OP cycle | Screens exist; approvals implicit (HOC/CEO inputs) | No monthly cycle, no versioned plan, no sign-off | Monthly five-step cycle with frozen snapshot and sign-offs (MOD-0190 fits) |
| G7 | Shelf life / FEFO | not covered; expired only as credit note (SOP-20) | No remaining-shelf-life rule for shipment or order sizing | Minimum remaining shelf life per market; FEFO allocation; expiry risk projection |
| G8 | Artwork change control | AO-13/14/24: artwork & mock-up upload/approve per order | No versioning, no change control, reuse rule unclear | Controlled artwork master with versions and change control, linked to SKU/market |
| G9 | Serialization / GS1 DataMatrix | AO-10: PDF of DataMatrix per order | No GTIN/serial/batch/expiry data model; file only | GS1 identifiers as data; serial number management; repository reporting where required |
| G10 | Batch release | AO-16: CoA upload + release by QC/PM/MD | Release not tied to quality status or qualified-person rules | Release as quality status change with QP/QA role and evidence |
| G11 | Distributor channel stock | SOP-16…25, 43 (sell-in, GIT, credit notes, wholesaler stock) | No module owns it in the blueprint | Ownership options in MODULE-MAPPING.md |

**Gap count: 11.**

## G1 — Safety stock

- **Current:** SS = daily sell-out (6-month average) × Σ maximum lead times − obligatory stock (= daily sell-out × Σ average lead times). Effectively daily demand × (max LT − avg LT). Obligatory stock is a separate lead-time cover.
- **Practice:** safety stock sized for a target cycle service level from demand and lead-time variability: SS = z × √(LT × σd² + d̄² × σLT²), where z = service-level factor (e.g. 1.65 for ~95 %), d̄ and σd = mean and standard deviation of demand per period, LT and σLT = mean and standard deviation of lead time in the same periods. Segment by ABC/XYZ; review quarterly.
- **Gap:** no variability, no service level, no segmentation; the max − average difference is only a proxy for lead-time risk and ignores demand swings.
- **Options:** (A) statistical formula with service level per segment — pros: explainable, scales, drives MOD-0191; cons: needs clean history (≥ 12 months) and lead-time actuals. (B) keep the current formula as a transitional rule, with a flag per SKU to switch — pros: no data prerequisite; cons: stays rough. (C) months-of-cover targets per segment — pros: simple for distributors; cons: static. **Recommended path: B now, A per segment when history exists.**

## G2 — Forecasting and accuracy

- **Current:** sell-out forecast typed by users; sales plan split per wholesaler must equal the budget total.
- **Practice:** statistical baseline (e.g. exponential smoothing with seasonality) plus a documented consensus/market-intelligence overlay; forecast value added measured; accuracy (MAPE/WMAPE) and bias tracked per level and lag.
- **Gap:** no baseline, no accuracy history, no lag-based measure, overrides not recorded.
- **Options:** (A) baseline + overlay with reason codes — pros: auditable; cons: model maintenance. (B) manual only but store forecast snapshots and measure accuracy — pros: cheap first step. **Recommended: B first, then A in MOD-0188.**

## G3 — Time fences and forecast consumption

- **Current:** unshipped/unordered forecast rolls into the next month (SOP-27, SOP-34); past months closed.
- **Practice:** demand and planning time fences (frozen inside the lead time, changes need approval; liquid outside); forecast consumed by actual orders/shipments within the period, not carried forward automatically.
- **Gap:** roll-over can double demand when the shortfall was real under-selling; no frozen zone protects production.
- **Options:** (A) time fences per SKU from lead times + consumption with explicit carry-forward decision — pros: stable production; cons: more settings. (B) keep roll-over but cap at one month and show it separately. **Recommended: A.**

## G4 — Lot sizing

- **Current:** ROUNDUP(net need / min batch) × min batch.
- **Practice:** per-SKU lot-sizing policy: lot-for-lot, fixed order quantity, minimum + multiple, maximum, period order quantity; economic order quantity where setup cost matters; shelf life caps the lot.
- **Gap:** no multiple or maximum; no shelf-life cap; one month of need only.
- **Options:** (A) policy attribute on the SKU/production setting (min, multiple, max, periods of cover) — pros: flexible; cons: data. (B) keep min batch + add shelf-life cap. **Recommended: A with B as default policy.**

## G5 — Capacity

- **Current:** none in the files; MVP6 MOD-0192 offers capacity plans, scenarios and bottleneck evaluation (backend, fixture-only).
- **Practice:** rough-cut capacity planning of the production plan against key resources/manufacturer capacity before orders are sent.
- **Gap:** order needs are sent to manufacturers without a capacity check.
- **Options:** (A) feed order/production needs into MOD-0192 scenarios — pros: reuses MVP6; cons: needs manufacturer capacity data. (B) manufacturer confirms capacity through the portal (ORDER CONFIRMATION status) — pros: simple; cons: late. **Recommended: B now, A later.**

## G6 — S&OP cycle

- **Current:** planning screens with manual inputs by HOC/CEO; no cycle or versioned plan.
- **Practice:** monthly cycle — data gathering, demand review, supply review, pre-S&OP (reconciliation, scenarios), executive S&OP sign-off; each cycle a frozen version with KPIs.
- **Gap:** no snapshot/versioning, no sign-off trail, no scenario comparison.
- **Options:** (A) use MOD-0190 snapshot + sign-off for each monthly cycle — pros: already built (backend); cons: needs demand/supply inputs (MOD-0188/0189). (B) calendar + checklist only. **Recommended: A.**

## G7 — Shelf life / FEFO

- **Current:** expired stock handled after the fact via credit notes (SOP-20).
- **Practice:** minimum remaining shelf life per market/customer at shipment; FEFO picking and allocation; projected expiry risk (stock older than cover); lot size capped by shelf life.
- **Gap:** no remaining-shelf-life rule in shipment or order planning.
- **Options:** (A) MOD-0176 rules consumed by MOD-0189 and allocation — pros: prevents write-offs; cons: needs batch expiry in stock. (B) report-only expiry risk. **Recommended: A.**

## G8 — Artwork change control

- **Current:** per order: artwork (RA) and mock-up (PM/PS/MD) upload + approve per component; mock-up disabled for the next order if one exists.
- **Practice:** artwork is a controlled document per SKU/market/version with change control (regulatory text, variation), approval workflow, and implementation date/first batch; orders reference the approved version.
- **Gap:** artwork lives on the order, not as a versioned master; change control and regulatory link missing.
- **Options:** (A) artwork master with versions + approvals (Workflow MOD-0023, Evidence MOD-0029), orders reference version — pros: GMP-aligned; cons: new ownership. (B) keep per-order approval, add version number and reuse rule. **Recommended: A; ownership options in MODULE-MAPPING.md.**

## G9 — Serialization / GS1 DataMatrix

- **Current:** a DataMatrix PDF is uploaded per order line.
- **Practice:** GS1 DataMatrix carries GTIN, serial number, batch and expiry; serial numbers generated/managed per batch; where required (e.g. EU FMD), data uploaded to the national/European repository and verified at dispense; aggregation where needed.
- **Gap:** no data model for GTIN/serials; a PDF cannot be verified.
- **Options:** (A) serial-number management linked to MOD-0174 batches and MOD-0290 GTIN — pros: compliance-ready; cons: market-specific rules. (B) keep file upload, add GTIN/batch/expiry fields. **Recommended: B now, A per market requirement.**

## G10 — Batch release

- **Current:** CoA upload and release tick by QC/PM/MD; cancel of release returns to production and keeps data.
- **Practice:** release is a quality status change (quarantine → released) by the authorised person (QP/QA) with documented evidence; stock is not available before release.
- **Gap:** release not linked to stock status; PM/MD releasing is not a quality role.
- **Options:** (A) release through MOD-0195 with status in MOD-0175/0173 — pros: GMP-aligned; cons: role change. (B) keep tick but restrict to QA role and block allocation until released. **Recommended: A.**

## G11 — Distributor channel stock

See MODULE-MAPPING.md (GAP-DCS). Practice: channel inventory visibility from distributor sell-in/sell-out data (EDI or portal), months of supply per channel partner, and reconciliation with invoices and credit notes.

# Module mapping — SCM planning requirements

🤖 Applying knowledge of @business-analyst + @product-manager. Source of module identities: `execution/registries/module-id-registry.md` (sha256 `1f217e17…`) and `execution/domains/supply-chain-execution/domain-config.md` (`c49ec37a…`). No module ID is created and nothing is decided here.

## 1. Requirements per target module (RULES.tsv, 60 rules)

| Module | Rules | Main content |
|---|---|---|
| **MOD-0188 Demand Planning** | SOP-15; SOP-24 (as demand input) | Sales plan split per wholesaler; sell-out forecast as the demand signal; forecast snapshots and accuracy (G2) |
| **MOD-0189 MRP & Replenishment** | SOP-26, 27, 29, 30, 33, 34, 39, 40, 41; AO-01, 02, 03, 04, 05, 07, 08, 09, 12, 20, 26, 27, 30, 32 | Shipment need/forecast, CWH and main-WH stock projection, production and order needs with lead-time offsets, lot sizing, order-forecast list and UI |
| **MOD-0191 Safety Stock Optimization** | SOP-37, 38; AO-03, 05 | Obligatory and safety stock, months-of-cover; statistical option (G1) |
| **MOD-0172 Allocation & ATP/CTP** (Commercial) | SOP-28, 31; AO-06 | Proforma-based shipment plan, reservations by country, available stock across markets |
| **MOD-0173 Inventory Ledger & Valuation** | SOP-30/31/32/39 (actuals), AO-06, 19 | Reserved / non-reserved stock actuals per warehouse |
| **MOD-0174 Lot/Batch/Serial Tracking** | SOP-36; AO-10, 15 | Production actual from batch creation; batch/expiry data; serial data (G9) |
| **MOD-0176 Expiry / FEFO** | none written in the files (G7) | Remaining shelf life, FEFO — recommended addition |
| **MOD-0194 Work Orders** | SOP-35; AO-17, 21, 32 | Production order/window, status flow up to production |
| **MOD-0195 Batch Execution / eBR** | SOP-36; AO-15, 16, 18, 24 | Batch data, CoA, batch release (G10) |
| **MOD-0197 Co-Manufacturer Management** | SOP-35; AO-09, 17, 18, 20, 21, 22, 23, 25, 26, 28, 29, 30 | Order to external manufacturer, statuses and actions, permissions, third-party production |
| **MOD-0148 Supplier Portal** | AO-25 | Manufacturer login to the order page (portal access pattern) |
| **MOD-0190 S&OP Workflow & Sign-offs** | — (G6) | Monthly cycle, frozen snapshot and sign-off — no rule in the files, recommended use |
| **MOD-0192 Capacity Planning** | — (G5) | Capacity check of order needs — no rule in the files |
| **GAP-DCS** (distributor channel stock) | SOP-16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 43 | Sell-in, GIT invoices, credit notes, wholesaler stock, sell-out, relocation |
| **GAP-ART** (artwork / mock-up) | AO-13, 14, 22, 24 | Artwork and mock-up per component with approvals |
| Other gaps | AO-10 (serialization, see 0174), AO-11 (registration status from Regulatory Affairs) | Consumed data, owner outside SCM |
| Open | AO-31 | Meaning unknown (OQ-AO-12) |

## 2. What MVP6 already covers

| MVP6 module | Covers | Does not cover |
|---|---|---|
| MOD-0190 S&OP Workflow & Sign-offs (backend, CT-accepted isolated package) | S&OP plan, immutable snapshot of input references, role sign-offs (`/api/supply-chain/sandop-plans/**`) | Any calculation in the two files; it consumes frozen DEMAND by reference and never becomes a demand source |
| MOD-0192 Capacity Planning (backend, bounded acceptance) | Capacity plans, scenarios, evaluations/bottlenecks | Shipment, stock, production or order calculations; production scheduling |

None of the 28 S&OP lines or the 32 Automatic_Order rules is implemented by MVP6.

## 3. GAP areas — ownership options (no decision)

### GAP-DCS — distributor channel stock (sell-in / GIT invoices / credit notes / wholesaler stock / sell-out)

| Option | Description | Pros | Cons |
|---|---|---|---|
| DCS-1 | Inside SCM: extend MOD-0188 Demand Planning with a channel-inventory sub-scope (sell-in consumed from O2C invoices, sell-out and wholesaler stock captured in SCM) | Close to demand planning; one planning owner | Invoices and credit notes are Commercial facts; risk of a second invoice record |
| DCS-2 | Commercial / O2C owns sell-in, GIT invoices and credit notes; SCM (0188/0189) consumes them read-only; wholesaler stock and sell-out captured in a Commercial channel scope | Each fact stays with its system of record | Two owners for one screen; needs a contract between domains |
| DCS-3 | New dedicated module (channel inventory / distributor management) through DCP-002 identity allocation | Clear ownership, can include distributor portal/EDI later | New ID, new pack, more governance; does not exist today |

### GAP-ART — artwork and mock-up approval

| Option | Description | Pros | Cons |
|---|---|---|---|
| ART-1 | Regulatory/Quality document control owns a versioned artwork master (Workflow MOD-0023 + Evidence MOD-0029); MOD-0197 orders reference the approved version | GMP-aligned change control; reusable across orders | Needs a regulatory/quality owner outside SCM |
| ART-2 | MOD-0197 Co-Manufacturer owns artwork/mock-up approval per order (as in the Excel) | Matches today's working method | Artwork tied to orders, weak version control |
| ART-3 | New module (artwork management) via DCP-002 | Dedicated lifecycle incl. printer/manufacturer proofs | New ID and pack |

Related consumed data: registration status (AO-11) and DataMatrix/serialization (AO-10) — owners outside SCM; recorded as dependencies only.

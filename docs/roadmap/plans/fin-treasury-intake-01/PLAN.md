# Treasury intake — MOD-0120 and MOD-0126, by re-expression

- date: 2026-10-11
- by: Control Tower
- source: `~/Projects/ERP-vNext/services/DitenErpTreasuryService` — **Archive / external reference**,
  authority tier 5 (`AGENTS.md:28`). It is evidence. It is never authority here.
- owner decisions taken 2026-10-11: re-express rather than lift · follow the Blueprint's placement and open
  only the domains the scope needs · start with MOD-0120 and MOD-0126 · raise the security findings separately
- evidence: `docs/records/audits/2026-10/erp-vnext-treasury-security-findings-01/REPORT.md`

## Why these two, and not the six

The source implements six modules. Their value is not evenly spread, measured by the inspection lane:

| MOD | src LOC | portable | the honest reason it is in or out |
|---|---|---|---|
| **0120** Accounts Receivable (AR) | 13,199 | ~50% | **IN.** Blueprint SoR for `invoices`. The richest business content in the service: deterministic projection bucketing with a three-level date precedence and policy versioning; dual-mode bulk import with an immutable rejection report; override lifecycle with threshold approval, SoD and concurrency; an evidence manifest with lock, supersede, legal hold and export-redeem. |
| **0126** Payments & Approvals | 7,230 | ~45% | **IN.** Blueprint SoR for `payment batches`. The best-reasoned module: SoD with tier-2 escalation and waiver-with-reason, a payment lifecycle state machine, workflow orchestration with instance-mismatch guards, 44 error codes with deliberate HTTP mappings. |
| 0124 Cash Positioning | 11,038 | ~40% | OUT for now. Blueprint SoR for `cash positions`, so it belongs eventually — but it has **no authorization at all** across 21 operations including publish, and four of its integration subsystems are self-declared scaffolds. Its permission catalogue is design work, not intake work. |
| 0125 Cash Forecasting | 1,978 | ~45% | OUT. Implements an AR-run input registry; the Blueprint makes it SoR for forecast versions, scenarios and variance analysis. Roughly 95% of the declared capability does not exist. Minting the id now would buy the name and not the thing. |
| 0146 Payment Run Support | 512 | ~15% | OUT, and note it is **Procurement / P2P** in the Blueprint, not Treasury — this repository's registry already says so deliberately (`:302`). A mapper and a controller over 0126. Worth revisiting once 0126 lands, and in the procurement domain. |
| 0119 Accounts Payable (AP) | 7,074 | ~55% | OUT for now. High portable share, but it is a derived control layer over invoices MOD-0120 owns, so it is cheaper after 0120 exists than before. |

## What the source is, stated plainly

43,213 lines of `src`, 88 operations, 24,071 lines of test across 702 cases. Of that, about **44% is portable
business logic, 34% is framework-bound and 21% is dead, scaffold or superseded**.

It has also never run end-to-end. In the configuration that ships, MOD-0120 cannot complete a single write:
every write path requires a successful audit emission, the registered sink returns failure unconditionally,
and no alternative implementation exists. The test suite asserts that as expected behaviour. So the audit
client is **build**, not port — and the same is true of the whole integration edge.

The right frame is *a well-reasoned specification with a partial implementation and no verified runtime*. The
rules are worth carrying. The code mostly is not.

## Gate 1 — identity, and it already passes

`verify_module_id.py` is fail-closed and discriminates, measured 2026-10-11:

```
MOD-0120  "Accounts Receivable (AR)"                exit 0   proven against Blueprint/registry
MOD-0126  "Payments & Approvals"                    exit 0   proven against Blueprint/registry

MOD-0120  "Accounts Receivable"                     exit 2   gate failed closed
MOD-0126  "Payment Proposals / Payment Approvals"   exit 2   gate failed closed
```

The second pair is the source's own naming. **Any pack authored with the source's names is refused.** Use the
Blueprint strings exactly, parenthetical included.

## The sequence

Each step names who may do it. CT does not widen its own authority, and three steps here are not CT's.

| # | step | who | gate it satisfies |
|---|---|---|---|
| 1 | Registry rows for MOD-0120 and MOD-0126 with the Blueprint names, suites and waves | **owner** (registry is central) | DCP-002, `AGENTS.md` §10 |
| 2 | Branch short codes for the two new domains in `AGENTS.md` §9, and the domains listed in §2 | **owner** (`AGENTS.md` is out of CT's scope) | `AGENTS.md` §8 Yol A |
| 3 | Domain scaffolds: `erp-suite-financial-management/` and `erp-suite-treasury-cash/`, each `README.md` + `domain-config.md` + `module-packs/` | CT, after 1 and 2 | §8 Yol A |
| 4 | A capability pack — next free id is **DCP-011** — stating the intake, the two modules, the excluded four and why | CT drafts, **owner approves** | CAP-001 §7 |
| 5 | Module packs for 0120 and 0126, `draft` first, carrying the **rules** extracted from the source with `path:line` back to it as reference | CT | §10 |
| 6 | Promotion of each pack to `ready-for-dev` | **owner** | §10: a draft authorises nothing |
| 7 | Port allocation — the source listens on **5058**, which this portfolio assigns to DevEnablement | **owner** | `AGENTS.md` §3 |
| 8 | Implementation, per module, in this portfolio's architecture | a DEV lane, after 6 | the usual |

**Steps 1, 2, 4, 6 and 7 are the owner's.** CT can prepare text for each and will, but cannot take them.

## What step 5 must carry, and what it must not

**Carry** — the rules, as specification, each with a `path:line` into the source so a reader can check CT's
reading: AR's date-precedence and policy versioning; the two import modes and the immutable rejection report;
the override threshold/SoD/concurrency triad; the manifest lifecycle including legal hold; 0126's SoD tiers
and waiver-with-reason; its lifecycle state machine; its 44 error codes and their HTTP mappings.

**Do not carry** — the response envelope, the base controller, the DI graph, the Mongo contexts and index
definitions, the API versioning scheme, the permission attribute, the config prefixes (five inconsistent
ones), and `"*"` as a permission wildcard. Those are rewritten against this portfolio's building blocks, and
the last one must never appear here.

**Do not carry the source's packs.** Four of six are `status: draft` and say in terms that they authorise no
runtime implementation — yet the code shipped anyway. They are useful reading and they are not precedent. The
source domain also carries a DCP-002 error this portfolio must not inherit: a pack filed as
`MOD-0122-bank-reconciliation.md`, where the Blueprint makes 0122 *Period Close & Consolidation* and
*Bank Reconciliation* MOD-0121.

**The tests** are 24,071 lines that never drove a host or a database. Their *assertions* are a usable
specification of intended behaviour. Their *harness* does not survive the response-envelope change. Plan for
the first and discard the second.

## Five upstreams this portfolio does not have

MOD-0289 bank account registry · MOD-0003 data contract registry · MOD-0269 bank feeds · MOD-0037 integration
run health · an AuthService `authz/principals` endpoint. Three more exist here but at different paths (audit,
workflow, evidence), and one capability — evidence-requirement evaluation, which MOD-0126 calls — has no
counterpart at all.

MOD-0126 depends on the workflow, evidence and audit seams. **Step 8 for 0126 cannot complete until those
three are resolved**, and one of them does not exist. That belongs in DCP-011 as a stated dependency, not
discovered during implementation.

## What CT has not done

No code has been read into this repository. No registry row, no domain, no pack and no DCP has been created —
steps 1 and 2 are the owner's and everything else waits on them. The inventory this plan rests on is one
lane's reading, spot-verified by CT on the identity gate, the Blueprint placements and the two security
findings; the portable/framework/dead percentages are that lane's judgement and are labelled as such.

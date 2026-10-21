---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# MVP-6 owner-queue — reality readout (v9.6 §5 mapped to measured state, 2026-10-18)

    author    : CT (gmg-10) · branch feature/mvp6-logistics @ 6e1e5cddd (pushed)
    purpose    : v9.6 §5 lists eleven "owner-queue" items as if all were open owner decisions. Measured
                 item by item this session, most are already decided, already applied, not a product gap,
                 or a correctly-held gate. This readout is the true state with evidence; it edits no plan.

## The eleven items

| # (v9.6 §5) | v9.6 wording | measured state now | evidence |
|---|---|---|---|
| 1 | Push the branch | **DONE** — pushed; branch now at `6e1e5cddd`, origin in sync | this session's pushes (first `06c4d4481`) |
| 2 | Q496 — grant the 25 keys | **NOT a product gap** — keys exist + self-register, a role holds them; it is a lane disposable-DB fixture + browser-only verification | `mvp6-q496-status-01` (`239cc2b60`) |
| 3 | Q484 — Capacity middleware re-pin | **DECIDED (Option 2′) + applied + verified** — env-gated to Testing; composition tests 2/2; hashes not re-pinned | `5a2d0085f` (decision), `1a4b93e81` (impl) |
| 4 | MOD-0192 `:199/:200` | **resolved by the Q484 decision** (Capacity context now composed in the test host). Pack `:199` prose still stale → folded into the pin reconciliation | via Q484; `mvp6-pack-pin-reconciliation-01` |
| 5 | Rows-3 gateway explicit routes | **superseded by OD-P1** — the proposal routed logistics to 5061/moved CRM to 5065; the tree is 5066/CRM-5061. Needs re-derivation on 5066 + OD-7/8/9/12 | `mvp6-gateway-routes-conversion-01` (`06c4d4481`) |
| 6 | Q322 — four UI pack revisions | **ready proposal**, per-pack owner approval + one narrow §22 decision; not CT | `mvp6-q322-status-01` (`5c2b073b4`) |
| 7 | Q498 — under-declared responses | **DONE** — additive responses applied, minor bump (shipment 3.3.0, sandop 3.1.0), parity 3/3 | `7bdf0110f` (apply), `61a550a46` (package) |
| 8 | MOD-0184 `:376` runtime-write | **live and accurate** (correctly-held gate, not stale) — owner release after VER; no CT action | `mvp6-mod0184-runtime-write-status-01` (`5f342e5f9`) |
| 9 | Q15 — integration execution | **owner decision DONE (A, 2026-09-27)**; open item is Q151 execution (integration-agent, READY, Q143 satisfied). v9.6 label stale | `mvp6-q15-status-01` (`6e1e5cddd`) |
| 10 | SupplyChain CI row | **prepared** (five files, two deny rules) before this session; integration/owner applies | `mvp6-supplychain-ci-row-01` |
| 11 | BL-650 — CI guard intent vs measure | **DECIDED (Option 1) + applied + verified** — guard follows decision D into the scripts layer; test 1/1 | `0ca26fb69` (apply), `874c81e67` (package) |

## Also closed/prepared this session (not in the eleven)

- **status_report.py glob defect** — applied + verified (footer 0→1); the REP-001 §7 instrument can now
  see lane records. `79b9e3402` / `39e608aed`.
- **BL-649** — timing-flake fix applied + verified (test off the wall clock). `f798b1cda` / `6763f58b1`.
- **pack↔contract pin reconciliation** — plan prepared (new hashes, provenance, per-pack inventory) as a
  follow-up from Q498/Q484. `56f32cd36`.

## What is genuinely still open — and whose it is

| item | nature | who |
|---|---|---|
| Gateway routes (item 5) | re-derive the explicit routes on **5066** + rule OD-7/8/9/12 | owner confirms port + the OD rulings → **then CT can produce the patch** (the one remaining item CT can build) |
| Q151 (from Q15) | execute the integration in one registered checkout + golden-flow slice | integration-agent |
| Q322 per-pack approval; the §22-qualifier call | approve each §11 revision; decide whether to strike the stale §22 "to be built" lines | pack owners |
| pin reconciliation apply | move each pack's "today" pin to 3.3.0/3.1.0 | pack owners |
| MOD-0184 runtime release | after UI/runtime VER completes | owner |
| Q496 browser cells | one lane fixture write + a browser pass (service-boundary evidence already stands) | owner |
| SupplyChain CI row (item 10) | apply the five prepared files (two deny rules: `scripts/**`, `tests/**`) | owner/integration |
| BL-652 | permission-coupling work package (its CarrierContractError half landed pre-session in `07f7e2b48`) | owner-scoped |

## The pattern worth naming

Of the eleven, **three were already decided** (Q484, Q15, BL-650), **one is not a product gap** (Q496),
**one is a correctly-held gate** (MOD-0184 `:376`), **one is a ready proposal** (Q322), **two are done**
(push, Q498), and **one is superseded** (gateway routes, item 5). Only the gateway re-derivation, the CI-row
apply, and the pure execution/owner-apply items are genuinely outstanding. The v9.6 owner-queue overstates
the open **decision** load because its text trails decisions already taken — the same stale-narrative family
caught at Q484 `:199`, Q15, and the pack pins. A v9.7, if cut, should read from this table.

## Files

- `READOUT.md` — this mapping. Edits no plan; `mvp6-development-plan-v9.6.md` stays as the last cut.

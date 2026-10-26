---
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E1
decision: conditional
commit: b972bb327
note: "no module field — this record is about a rule, not a module. That absence is one of the findings."
---

# REP-001 §6 cannot be applied to a record that already exists — it collides with K4, and with the seals

    lane      : ct-rule-collision-rep001-k4-01
    measured  : 2026-10-17
    scope     : REP-001 §6/§7 against K4 (docs-organization.md) and the lane ARTIFACTS.sha256 seals
    verdict   : DECISION REQUIRED. §6 has never applied to anything and now cannot be applied to 27 of
                this month's 55 lane reports without breaking a dated seal.
    requested : follows from ct-discipline-and-readiness-audit-2026-10-17-01 §C
    method    : static. No record was edited; no seal was touched.

## The collision, in one paragraph

REP-001 §6 requires every CT decision record under `docs/records/audits/<yyyy-aa>/` to carry a
front-matter künye, and states its purpose: *"Künye insan için değil, §7'deki betik için vardır:
kararların listesi elle sayılmaz."* K4 (`docs-organization.md`) says
*"`records/` yazıldıktan sonra düzeltilmez"* — a written record is never corrected; a wrong one is
superseded by a new one. And each lane's `ARTIFACTS.sha256` hashes its own `REPORT.md`.

So adding the künye §6 demands to a record that already exists **changes bytes K4 forbids changing and
breaks a seal the lane published.** §6 is therefore applicable only to records created after it — and
it was never applied to those either.

## Measured

| | |
|---|---|
| record `.md` files under `docs/records/` | **1203** |
| carrying the `evidence_level` künye | **1** — written today, by this audit's predecessor |
| 2026-10 lane `REPORT.md` files | 55 |
| of those, hashed by their own `ARTIFACTS.sha256` | **27** — künye impossible without breaking the seal |
| not hashed | 28 — künye addable freely |

**The five records the readiness table cites are all in the sealed group:**
`mvp6-r4a-carriers-ui-01`, `mvp6-r4b-loads-ui-01`, `mvp6-r2-returns-ui-01`,
`mvp6-r4c-claims-ui-01`, `mvp6-q446-gate-18-0-four-modules-01`.

So the backfill that would make §7's script produce a real table is impossible exactly where it would
matter, and possible only where it would not. Filling the 28 peripheral records and leaving the five
authoritative ones empty would produce a table that **looks** populated and is not — which is worse
than the honest `kayıt yok` the script prints today.

## A second, independent reason the script finds nothing

`module_code_of` needs a single `module:` field or one MOD code in the path. Of the 55 lane reports,
**4** name exactly one module in their path. Most CT lanes are cross-module — the discipline audit
covers nine, this record covers none — so even a künye-compliant lane is skipped. The record you are
reading carries a künye and is still invisible to the script; its `module:` field is absent because
there is no honest value for it, and inventing one would be the thing §5 calls term-invention.

## Proposal — a side-car, so the künye never touches a sealed record

Add `KUNYE.yaml` beside `ARTIFACTS.sha256` in each lane, and have §7's script read it in preference to
front matter:

```yaml
work_package: WP-MVP6-LOGISTICS-01
evidence_level: E4
decision: accepted
commit: 4a8d4d4b
modules: [MOD-0184]          # a LIST: a cross-module lane is the normal case, not the exception
```

Why this and not the alternatives:

- **It does not touch `REPORT.md`,** so K4 holds and no seal breaks. A new file in a lane directory is
  not a correction of the record; it is an addition beside it.
- **A `modules:` list** fixes the second cause in the same change. One record can then register the
  several modules it actually decided.
- The lane's own `ARTIFACTS.sha256` can hash `KUNYE.yaml` from the day it is written, so the künye is
  sealed like everything else — it is only the *retrofit* onto already-sealed reports that is barred.

Cost: a glob default change and a file read in `scripts/status_report.py`, plus one §6 amendment.
**`scripts/**` is denied to CT**, so both are the owner's.

## Three ways to go, and what each costs

| # | option | cost | what it buys |
|---|---|---|---|
| 1 | **the side-car above** | ~2 lines of script, one rule amendment, then CT backfills 55 lanes mechanically | §2's table becomes producible for the first time, including for the five sealed records |
| 2 | künye only on records created from now on | nothing | the table fills over months; today's state stays `kayıt yok` for every module |
| 3 | amend §6 to drop the künye and let §7 read the lane's own files | more script work | avoids a new convention, but has to infer levels CT currently states in prose |

**CT recommends 1** and did **not** start any backfill, because 2 and 3 make the work CT would do
pointless, and because the 28-record half-fill would be actively misleading.

## What this record does not claim

That the hand-written status numbers were wrong. They were measured; they were just not produced by
the instrument the rule names, and the readiness audit already records that as a finding against CT.
This record is about why the instrument cannot be fed, not about the numbers.

# Correction to `dc0f64f95`, and the control-activation decision

- date: 2026-10-04
- owner: ny@gmgroup.ch
- status: ACCEPTED
- ledger: Q407, Q408, Q409
- history: **not rewritten.** The commit stands; this record is the correction.

## 1. Correction to `dc0f64f95`

**Original assertion, in the commit message now in HEAD:**

> "The evidence kit (k00-k11) and the per-module smoke scripts lanes have been running all
> along without them being in any commit, so every lane result so far was produced by
> tooling nobody else could reproduce."

**Measured evidence, 2026-10-04:**

| | |
|---|---|
| kit outputs in October's records (`CLEANUP.tsv`, `PORTS.tsv`, `REDACT*.tsv`) | **0** |
| lane-local harnesses in the same records (`fixture.py`, `start.sh`, `run-suites*.sh`) | **7** |
| kit phases marked `CANDIDATE — NOT ACTIVE` | **15 of 15** |

**Corrected conclusion.** The evidence kit existed and was wired through `run-kit.sh:30,:73`,
but was never operationally adopted by any inspected lane. The reproducibility problem the
commit message pointed at is real — and its cause is **lane-local harness divergence**, not
the kit's absence from committed history. R-2 recovering the stack recipe by reading Q371's
record folder is that divergence in action.

**Impact.** The commit message contains an unsupported causal statement. The committed
implementation itself is not invalidated by this correction unless separately demonstrated.

**Why history is not rewritten.** Auditability and traceability are the governing principle.
A wrong claim, its measurement and its correction, all preserved, is a stronger audit trail
than a tidy history.

## 2. Q409-A — ACCEPTED, applied today

The lane closure obligation is now **K22** in the SOP's iron-rule register, the single
canonical invariant register, and §39's Closure checklist **references** it rather than
restating it. It is not copied into the 20 agent contracts: duplicating a rule across
layers creates shadow authority and the copies go stale.

## 3. Q409-B — NOT ACTIVATED

The evidence kit stays `CANDIDATE`. Declaring it `ACTIVE` on the strength of existing,
correct, wired code is the exact error this programme has now made five times in one day,
and is what **K23** is written to prevent.

Required lifecycle: **CANDIDATE → QUALIFICATION → PILOT → ACTIVE**, never
`CANDIDATE → ACTIVE`.

A pilot lane must demonstrate equivalence end to end: source validation, deterministic
setup, port ownership, secret and redaction scan, DB setup and diff, evidence output, and
`k11_cleanup` actually running. It must also answer one adversarial question that the
current wiring leaves open: **can a lane be marked CLOSED when cleanup failed?**

Treated as its own change package, after MVP-6. It does not race the closure.

## 4. The 28 GB — one-time controlled remediation, not "freeing disk"

Recorded as a procedure so it does not blur into Q409-B:

`inventory → ownership verification → evidence verification → PID/port verification →
cleanup → disk delta verification`

Scope: `q84b` (11G), `q88b` (5.8G), `q65b` (5.8G), `q185` (5.7G). Verified before the
decision: all four idle, no process, and their evidence tracked in HEAD at 268, 229, 188 and
126 files. `base` (5.9G) is a shared baseline copy, not lane residue, and is **out of scope**.

## 5. The finding this names

**Implemented-but-Inactive Control Gap.** Five instances in one day — Q217/Q236, Q271/Q272,
Q363, Q387, Q407 — share one root: presence in the repository was read as enforcement on the
execution path. §18.0 already forbids an operational-looking shell without real behaviour.
K23 applies the same principle to engineering controls: **a control shell is not completion
either.**

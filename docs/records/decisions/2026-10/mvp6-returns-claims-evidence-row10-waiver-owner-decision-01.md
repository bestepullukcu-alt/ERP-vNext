# Owner decision — §18.0 audit/evidence waived for MOD-0186 and MOD-0187

- date: 2026-10-05
- owner: ny@gmgroup.ch
- status: ACCEPTED
- ledger: Q446, Q455
- precedent: `docs/records/decisions/2026-10/mvp6-shipment-pod-evidence-row10-waiver-owner-decision-01.md`

## What is waived

SOP §18.0's audit/evidence row — *"kritik mutation audit edilir; **regulated flow shared
evidence service kullanır**"* — is **waived for MOD-0186 Returns and MOD-0187 Claims**, for the
`evidenceReferenceIds` surface only.

## Why the same reasoning as MOD-0183

MOD-0183's waiver was granted because *"this row prescribes references and no binding to an
evidence service"* — the pack instructs the module to store opaque references, so the module
was being measured against something it was never built to do.

CT measured the same shape in both modules: Returns and Claims store `evidenceReferenceIds`
and there are **zero** resolve-or-validate sites for them anywhere in the service. Identical to
POD.

Q446 found the row NOT MET for both, correctly, because MOD-0183's waiver covers POD only.

## Scope, and it is deliberately narrow

| | |
|---|---|
| modules | MOD-0186 and MOD-0187 only |
| row | §18.0 audit/evidence, the shared-evidence-service clause only |
| **not waived** | the audit half of the row. Critical mutations are still audited, and both modules were measured doing so |
| owner | ny@gmgroup.ch |
| **expiry** | **MVP-6 closure** — the same expiry as MOD-0183's, so the three expire together |

The row is recorded as **CLOSED UNDER WAIVER, not MET**. A measurement states it that way
while MVP-6 is open, and measures the row on its merits after closure without a new decision.

This is not a blanket waiver. Two named modules, one clause, one expiry. K19: an exception
without an owner and an expiry becomes a permanent shadow standard.

## What must be re-decided at MVP-6 closure

Three waivers expire together and the question is the same for all three: does a shared
evidence service exist, and should these references bind to it? If the answer is still no, the
packs should say so rather than the waiver being renewed a third time.

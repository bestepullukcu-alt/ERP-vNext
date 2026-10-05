# Owner decision — explicit gateway routes for the shipment-bundle family

- date: 2026-10-05
- owner: ny@gmgroup.ch
- status: ACCEPTED, **sequenced after the origin/main merge**
- ledger: Q397, Q446, Q456

## Decision

The five modules get **explicit gateway routes**, and the `/api/shipment-bundle/{everything}`
catch-all is **removed**. The gateway's other catch-all routes are not touched.

## Why the packs are right, and it is not a style question

MOD-0186:749 and MOD-0187:699 require explicit routes with OPTIONS (NET-001), header
passthrough, **and `/returnsXYZ` not matched**.

That last clause is the substance. The catch-all matches `/api/shipment-bundle/returnsXYZ`
today, so a garbage path is forwarded downstream instead of answering 404. That is a real
behavioural defect, not a preference.

## Why the scope stops at this route family

CT measured: **135 of the gateway's 271 routes are catch-all**. The pattern is established
across the whole gateway, and changing it everywhere is not MVP-6's work. Within this family
the pack asks for the opposite and its reason holds, so this family changes and nothing else.

## Why it waits for the merge

Q435 measured that `ocelot.json` **auto-merges silently** — no conflict marker appears. Routes
written now could be altered by the merge without anyone seeing it. Doing this after the merge
costs one pass; doing it before costs two and risks a silent loss.

## Acceptance

Explicit routes for all five modules with OPTIONS and the five headers passed through; the
family catch-all gone; `/api/shipment-bundle/returnsXYZ` answering 404; every existing
shipment-bundle path still answering as it does today, proven by a gateway smoke.

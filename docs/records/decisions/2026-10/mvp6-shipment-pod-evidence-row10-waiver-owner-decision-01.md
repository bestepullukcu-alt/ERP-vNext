---
decision_id: MVP6-SHIPMENT-POD-EVIDENCE-ROW10-WAIVER-OWNER-DECISION-01
status: approved (waiver, K19 fields filled) — expires at MVP-6 closure
decided_at_local: 2026-10-03 (the ledger row gives the date only; no time was given)
decided_by: the repository owner (as named in the ledger row; this record adds no name)
decision_source: owner decision 7 of 9, 2026-10-03, recorded by CT only as ledger row Q255
approvedBy: owner decision recorded in CT-QUEUE row Q255
bound_to: docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv sha256 b8cdc8e6c559da6bdaa0d85bb6e38b5085608d945e728c5ac447526b70a9ec71 (the ledger file at the time of recording; it is append-only and will move); row Q255 alone (the full tab-separated line, newline included) sha256 dcf6376201f1d79c76514e8b4d711590861955112cc75df94f332e21fdb4f778
recorded_by: Q334 (documentation-writer, Claude Code lane) on 2026-10-03 on CT instruction; this record makes no decision of its own
---

# MOD-0183 Shipment — SOP §18.0 row 10, shared evidence service for POD evidence — WAIVED until MVP-6 closure

## Decision

The owner **waived** SOP §18.0's shared-evidence-service requirement for **MOD-0183 POD evidence**.

| K19 field / item | value |
|---|---|
| What is waived | SOP §18.0's shared-evidence-service requirement, for MOD-0183 POD evidence only |
| Against which standard | SOP §18.0 slice gate, row 10 (Audit / Evidence) |
| Owner | the repository owner |
| Expiry | **MVP-6 closure.** The waiver does not survive into MVP-7 and must be re-decided there |
| Effect on the gate | Row 10 is **CLOSED for MVP-6 under waiver, not MET** |

## Exact decision text (source: CT-QUEUE row Q255, `state` column)

> OWNER WAIVED 2026-10-03 (decision 7 of 9), WITH K19 FIELDS. SOP 18.0's shared-evidence-service requirement is waived for MOD-0183 POD evidence. OWNER: the repository owner. EXPIRY: MVP-6 closure - the waiver does not survive into MVP-7 and must be re-decided there. REASON: the pack PRESCRIBES free-text references ('reference only, no binary upload', pack:110,207), so this is a gap against SOP 18.0 and NOT a deviation from the pack; forcing a real evidence-service binding would widen MOD-0183's scope mid-wave. Gate row 10 is therefore CLOSED for MVP-6 under waiver, not met

## The question it answered (source: CT-QUEUE row Q255, `item` column)

> C-05: OWNER RULING NEEDED, not code. POD evidence is free-text references nothing resolves, and the service has no client to any evidence/document service - but the PACK PRESCRIBES EXACTLY THIS (reference only, no binary upload, pack:110,207) while the readiness record calls POD regulated. So it is a gap against SOP 18.0, not a deviation from the pack. Owner either waives 18.0 here (K19: needs owner + expiry) or amends the pack to require a real evidence binding

Both quotations are copied byte-for-byte from the row; the hyphens and the missing final full stop are the source's.

## Reasoning (from the source)

The pack prescribes free-text POD evidence references. Today it states the rule in two places:
§4 `EvidenceReferenceIds` — "At least one immutable document/evidence reference; binary content not stored here"
(pack line 110), and §12 `EvidenceReferenceIds` — "At least one" / "Reference only; no binary upload" (pack line 362).
The code follows the pack. So the missing evidence-service binding is a gap against SOP §18.0, not a deviation
from the pack. Binding a real evidence service now would widen MOD-0183's scope mid-wave.

*Citation note:* the ledger cites `pack:110,207`. Line 110 still holds the §4 rule. Line 207 now holds §8.1 text,
because §8.1 and §8.2 were added above §12 on 2026-10-03. The §12 rule the ledger quotes ("reference only, no binary
upload") is now at line 362. This record leaves the ledger as it is and gives today's lines here.

## Closed under waiver is not MET

A measurement of SOP §18.0 row 10 for MOD-0183 records **CLOSED UNDER WAIVER** while the expiry condition is unmet.
That is not MET:

- **What is still true:** POD evidence is free-text reference strings that nothing resolves or verifies, and the
  service has no client to any evidence or document service. The waiver does not change this.
- **What the waiver does:** it makes the gap acceptable for MVP-6 under a named owner and a named expiry. It does
  not make the requirement satisfied.
- **What Q332 found:** Q332 (`docs/records/audits/2026-10/mvp6-q332-mod0183-conformance-01/GATE-18-0.tsv`, row 10)
  marked row 10 NOT MET under both readings and found no waiver. Under the information it had, that was correct.
  This record exists so that the next measurement can find the waiver.

## Scope — what is not waived

- Only SOP §18.0's **shared-evidence-service** requirement, and only for **MOD-0183 POD evidence**.
- Not waived: the rest of row 10. Critical mutations must still be audited. Q231 and Q332 measured this half as
  met in code (`ShipmentRepository.cs:92-93`, `ShipmentTests.cs:120`).
- Not waived: any other §18.0 row, any other module (including MOD-0184 to MOD-0192), any other standard.
- Not waived: pack §8.2. Its obligations on the confidential PII inside stored Warehouse snapshots, and its open
  retention decision, stand unchanged.
- No acceptance box is ticked by this record, and no pack criterion becomes MET because of it.

## At expiry

- **Condition:** MVP-6 closure. A reader tests it by asking whether MVP-6 has closed. This record sets no calendar
  date and does not define the closure event. It uses the ledger's term unchanged.
- **When the condition is met:** the waiver lapses and does not carry into MVP-7. It must be re-decided there
  (Q255). A measurement of row 10 for MOD-0183 taken after MVP-6 closure has no waiver to apply unless a new owner
  decision exists. Without one, it measures the row on its merits.

## Where else this is recorded

- Pack: `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` §12. This is the
  section that states the POD evidence rule (`EvidenceReferenceIds`, "Reference only; no binary upload"). The waiver
  line sits directly under the §12 table and points here.
- Ledger: `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` rows Q255 (the decision) and Q334 (this recording).

Source rows are not edited (K4).

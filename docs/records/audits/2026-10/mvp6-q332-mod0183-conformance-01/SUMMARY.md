# Q332 — MOD-0183 conformance against its pack (measurement only)

- Measured: 2026-10-03 (preflight `Sat Oct  3 15:24:26 UTC 2026`), branch `feature/mvp6-logistics` @ `4a8d4d4b3`,
  703 dirty paths, 0 staged.
- Pack: `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md`
  - sha256 at start: `6e2af50f954b54e5fa490e08a11d3b7e5411ba8d81e756c52a02787c81c9c990` (578 lines, 35 headings)
  - sha256 at end: `6e2af50f954b54e5fa490e08a11d3b7e5411ba8d81e756c52a02787c81c9c990` — unchanged during the measurement
- Baseline: `docs/records/audits/2026-10/mvp6-q231-mod0183-18-0-gap-01/SLICE-GATE-MATRIX.tsv` (8 MET / 7 NOT MET).
- Method: static only. No code changed, no pack edited, no service or mongod started, no build, no test run.
- Verdicts: MET · NOT MET · UNDETERMINED (not settled by static evidence in this WP) · REQUIRES RUNTIME (only a
  started service, mongod or mock can settle it).
- **This is a measurement. CT rules. No disposition is recorded and no acceptance box is ticked.**

## Strictness rule applied

`verdict` = lenient reading: the code or the pack text satisfies the criterion as written.
`verdict_strict` = strict reading, applied the same way to every row:
1. the criterion must hold at the boundary a user meets (gateway, not only service);
2. the declared code must be asserted, not only the status;
3. evidence must be persisted in a record or covered by a persisted test run. The latest persisted run is Q266
   (`docs/records/audits/2026-10/mvp6-q266-testenv-01/PER-MODULE.tsv`: TOTAL 417/1/418, Shipments 75/75). Tests added
   after it (telemetry O-3/O-4, manifest M-01/M-05) are `REQUIRES RUNTIME` under the strict reading.

## Part 1 — SOP §18.0 (15 rows) — `GATE-18-0.tsv`

| reading | MET | NOT MET | UNDETERMINED |
|---|---|---|---|
| lenient | 10 | 5 (1, 2, 3, 10, 13) | 0 |
| strict | 4 (4, 5, 6, 11) | 10 | 1 (14) |

Strictness-dependent rows (6): 7 Validation, 8 RBAC/Tenant, 9 Data classification, 12 UX states,
14 Do-not-change, L10n. Under neither reading do 1, 2, 3, 10 or 13 pass.

Row 12, kept apart as asked: **defects closed** — yes, all four Q231 DEFECT rows have code in the working tree (Q329).
**States measured** — no: no persisted record shows the views rendered in each state; Q264 could not measure 15
view-state rows; Q329's browser observations have no record folder.

Row 13: O-3 4/4 in code and tests; O-4 3/3; no exporter (pack rules it not a gate, §8.1 :206-207); p95 never reported
(pack :208); O-1 NOT MET end to end and its web half not re-measured. The pack's own gate sentence (§8.1 :189,
"when, and only when, all four exist") keeps row 13 NOT MET under both readings.

## Part 2 — pack criteria (125 rows, one per criterion) — `PACK-CRITERIA.tsv`

| section | rows |
|---|---|
| §16 | 14 · §17 18 · §18 12 · §12 13 · §13 9 · §13.1 12 · §8.1 8 · §8.2 4 · §14 7 · §15 1 · §22 22 · §9 1 · §11 4 |

| reading | MET | NOT MET | REQUIRES RUNTIME | UNDETERMINED |
|---|---|---|---|---|
| lenient | 101 | 20 | 2 | 2 |
| strict | 68 | 36 | 14 | 7 |

34 rows change with strictness; they are the rows where `verdict` ≠ `verdict_strict` in the TSV.

## Changes since Q231

- Row 7 Validation: NOT MET → MET (lenient). Q329 closed both client gaps and the 2000/1000 mismatch in code.
- Row 12 UX states: NOT MET → MET (lenient) for "defects closed"; still NOT MET for "states measured".
- Inputs moved, verdict did not: row 2 (route, provider and nav keys now in the working tree; registration and
  permission seed absent; nothing committed), row 3 (UI contract now in pack; 500 code fixed; 403 still
  `INVALID_REQUEST` at `HasPermissionAttribute.cs:16`; Q218 pin open), row 13 (O-3/O-4 added), L10n (63 → 64 keys).
- Rows 8, 9, L10n were MET in Q231 and stay MET under the lenient reading; Q231 did not apply the strict reading.
  Row 9 strict is new because §8.2 was added to the pack after Q231 (Q314).

## Pack-vs-tree drift found (not self-contradiction; reported, not fixed)

- §9 :275 says 63 keys; tree has 64.
- §11 open items 2 and 3 (:340-345) describe defects that the working tree no longer has.
- §22 :543 "0/7 present today" vs §11 :336-339 "21 of 21": the pack itself explains it (committed tree vs working
  tree). Both readings are stated by the pack; recorded in the nav-key rows, not ruled on. Not treated as a STOP.
- §22 provider now matches the pack (Q245's SHIPMENTS_CREATE / missing `:guid` findings are superseded).

## Disagreement with CT

1. **Q245 premise.** The dispatch says Q245's results were never written anywhere. They were:
   `docs/records/audits/2026-10/mvp6-q245-ver-0183-01/` (7 files; ACCEPTANCE-MATRIX.tsv 105 rows) and its
   ARTIFACTS.sha256 verifies. Q332 did not reproduce Q245, as instructed.
2. **Suite numbers.** CT's 432 passed / 1 failed / 433, Shipments 90/90, is not in any record found. The latest
   persisted result is Q266: 417/1/418, Shipments 75/75. Q332 could not re-confirm: the suite needs a running mongod
   and writes under `services/**`, both forbidden here. Not re-confirmed, not quoted as fact.
3. **Gate counts.** CT: 12 MET, 1 waived, 2 NOT MET. Q332: 10 MET / 5 NOT MET (lenient), 4 / 10 / 1 (strict). No
   waiver record was found, so no row is recorded as waived. Even if one of the five lenient NOT MET rows were waived,
   CT's count needs two more rows MET than Q332 finds. Candidates where CT's reading could differ: row 3 (if the 403
   code and the Q218 pin are not counted) and row 10 (if pack-prescribed "reference only" evidence is accepted as
   §18.0 evidence). Row 13 cannot be MET by the pack's own sentence while O-1 is NOT MET. Q332 does not know CT's
   per-row list and does not rule on it.

## Not done

- Test suite not run (needs mongod — STOP rule "criterion needing a started service").
- Prism mock smoke, gateway header propagation, O-1 web half, p95 reading, UX state rendering: REQUIRES RUNTIME.
- DB-010 and DCP-009 text not re-read; `ShipToReference` rule line and `RecipientName` trim not located — UNDETERMINED.
- §13.1 "Counted" column verified only for `IntakeBlocked` (:78) and `SourceDrifts` (:87), not per row.

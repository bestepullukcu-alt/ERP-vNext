# Q233 — The two re-pins (PROPOSED — not applied; the Edit tool was refused)

## Hash verified against disk before anything was written

| File | sha256 computed in this lane | `info.version` |
|---|---|---|
| `docs/analysis/contracts/shipment-bundle.openapi.yaml` | `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` | 3.1.0 (`:13`) |

Equal to the value Q218 measured and to the guard authority `docs/reference/architecture/docs-path-authority.json:11`.
The old pin, 3.0.0 `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`, is the hash of the one recovered copy
(`docs/records/audits/2026-09/mvp6-combined-final-release-pack-01/publication/docs/analysis/contracts/shipment-bundle.openapi.yaml`, F-Q218-1).

## Proposed pack text — before and after

| Pack | sha256 before (file in the repo, unchanged) | sha256 after (proposed text) | Lines before → after | Proposed text | Diff |
|---|---|---|---|---|---|
| `MOD-0186-reverse-logistics.md` | `933e89262713f36982a7885b9b95a84e3fbfad62b276b721a300c0ec6c8521fe` | `2c1a57573307dcd5ad0dcdf30804fd24b100c658fc4deab0596f783226e3c9f9` | 868 → 872 | `proposed/MOD-0186-reverse-logistics.md.txt` | `proposed/MOD-0186-reverse-logistics.md.diff.txt` |
| `MOD-0187-claims-management.md` | `3d1a00e2f0a0e7e58d19f44b340de9ab0d6bf84e7e2997db451c65d2ddb74cae` | `530b7a022b7bda50f442146710b0fb06408dffb9d761c373fbd3f0f046cd085a` | 819 → 823 | `proposed/MOD-0187-claims-management.md.txt` | `proposed/MOD-0187-claims-management.md.diff.txt` |

## What changes in each pack (seven changes each)

| # | MOD-0186 line | MOD-0187 line | Change | Task |
|---|---|---|---|---|
| 1 | 10 (`status_note`) | 10 (`status_note`) | adds: pin moved to 3.1.0 on 2026-10-02, no material change, with the section and record that say so. `status` itself is untouched | A1 |
| 2 | 584 | 533 | row label "Published contracts at acceptance and today" → "Published contracts at acceptance (date)"; the 3.0.0 hash stays, now labelled 3.0.0 | A2 |
| 3 | new row after 584 | new row after 533 | "Published contract today — pin in force": 3.1.0 `6dc1dd48…96aa2`; annex hashes unchanged; says the old label was untrue from 2026-09-26, citing F-Q218-5 | A1, A2 |
| 4 | 586 | 535 | "3.0.0 is published and bound" → "was published and bound at acceptance; the pin in force today is 3.1.0" | A2 |
| 5 | new paragraph after 592 | new paragraph after 541 | re-pin note: no material change for the module, citing F-Q218-2, F-Q218-3, `FIELD-PARITY.tsv`, `VERSION-DELTA.md`; states it fixes nothing else and that F-Q218-7 and "Still open" stay open | A3 |
| 6 | 619 (§32.3) | 569 (§32.3) | bound contract "3.0.0 (`5dfe7c1b…d21c`)" → "3.1.0 (`6dc1dd48…96aa2`; re-pinned from 3.0.0 …)" | A1 |
| 7 | 780 (§32.13) | 729 (§32.13) | the gap entry "Loads 3.1.0 forward drift of the `5dfe7c1b…` pin" is taken out of the open list and written as CLOSED 2026-10-02, with what closed it | A4 |

## What is deliberately not changed

| Line | Why it stays |
|---|---|
| MOD-0186 `:165-168`, `:480-481`; MOD-0187 `:166-168`, `:476-477` | historical 1.1.0 and 2.0.0 pins, already marked superseded by each pack's §31 |
| MOD-0186 `:484`, `:564`; MOD-0187 `:501`, `:515` | they state which bytes governed the September dispatch and acceptance. That was `5dfe7c1b…` and still is the acceptance record |
| every annex hash, every other hash | rule: no other hash, no other pin |
| `status: ready-for-dev` in both | rule: promotion is an owner decision |
| every checkbox | none ticked, none added (asserted by the generator: counts of `- [x]` and `- [ ]` equal before and after) |

## What the re-pin does not do

It does not make either module reachable, does not fix the consumer/contract gaps of F-Q218-7, and does not replace the acceptance
given on `5dfe7c1b…`. It makes the packs name the file that is on disk.

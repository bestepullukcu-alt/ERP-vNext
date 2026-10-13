# verify from: /Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-10/mvp6-q380-contract-repin-01 — `shasum -a 256 -c ARTIFACTS.sha256`

# Q380 — MOD-0186 and MOD-0187 re-pinned to SHIPMENT-BUNDLE 3.1.0

- Lane: Q380, documentation-writer, single writer on the MOD-0186 and MOD-0187 packs. Recorded 2026-10-04.
- Preflight: `Sun Oct  4 09:28:51 UTC 2026` · `feature/mvp6-logistics` · HEAD `c1f2dffe8` · porcelain 26 · staged 0 ·
  ledger row present (CT-QUEUE.tsv:482).
- Step 0: `AGENTS.md`, `git-safety.md`, `docs-organization.md`, and the Q218 record (its `ARTIFACTS.sha256`: 0 mismatches).
- **The contract file was not touched. No code touched. Nothing staged or committed.** Agent verdict ≠ CT ACCEPTED.

## The premise, re-measured before acting

Q218's finding holds: the Returns and Claims reads are identical in 3.0.0 and 3.1.0.

| | 3.0.0 | 3.1.0 (canonical) |
|---|---|---|
| file | `docs/records/audits/2026-09/mvp6-combined-final-release-pack-01/publication/docs/analysis/contracts/shipment-bundle.openapi.yaml` | `docs/analysis/contracts/shipment-bundle.openapi.yaml` |
| sha256 (recomputed) | `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` | `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` |
| `info.version` | 3.0.0 | 3.1.0 |

`evidence/compare-ops.py` (re-runnable, K7) compares each of the 17 operations and everything it reaches through
`$ref`. Only the three Loads operations differ (`queryLoads` in a reachable schema; `createLoadPlan` and
`transitionLoad` in operation text). **`createReturn`, `queryReturns`, `transitionReturn`, `createClaim`, `queryClaims`,
`transitionClaim`, `getShipment` and `queryCarriers` are EQUAL, operation and everything reachable.** The path set is
equal. Output: `evidence/compare-ops.txt`.

The annexes on the same lines are unchanged and still equal their pins (`evidence/digests.txt`): Returns
`00990a28…8a11`, Claims `16e65c26…4eb63`, root `7d1327a1…b8af`.

## What MOD-0183 pins

Nothing by version or digest: `grep -c 5dfe7c1b` and `grep -c 6dc1dd48` are both 0. It binds the contract by **path**
— "The frozen file `docs/analysis/contracts/shipment-bundle.openapi.yaml` is implementation authority" (`:134`) — and
by wire version "`SHIPMENT-BUNDLE` v1" (`:77`, `:151`, `:427`, `:456`). Wire `contractVersion` is `v1` in both 3.0.0
and 3.1.0, so MOD-0183 already tracks the canonical 3.1.0 and has nothing to re-pin. It is not this lane's file.

## Which occurrences were re-pinned, and which stay — and why

The authority this WP executes (Q218's adopted decision, above CT's dispatch in its own contract) says: "The
acceptance value should stay visible: both modules were accepted on `5dfe7c1b…`. Only 'today' changes." So every
**effective** pin moved to 3.1.0, and every line that **records history** keeps the hash it recorded.

| pack:line (pre-edit) | role | action |
|---|---|---|
| MOD-0186 `:10` status_note "on published … 3.0.0" | effective binding | now "accepted on … 3.0.0 and re-pinned to 3.1.0 / wire v1 (§31, Q380)" |
| MOD-0186 `:484` §28 "Final-pack delta binding (candidate until CT disposition)" | history (§31 itself calls it historical) | **kept** `5dfe7c1b` |
| MOD-0186 `:564` §30 PHASE15-CLOSE-01 grant record | history | **kept** `5dfe7c1b` |
| MOD-0186 `:584` §31 "Published contracts at acceptance and today" | the false sentence Q218 named | split: "at acceptance" keeps `5dfe7c1b`; new row "today (Q380)" pins 3.1.0 `6dc1dd48…` with its reason |
| MOD-0186 `:586` "SHIPMENT-BUNDLE 3.0.0 is published and bound." | the WHY sentence | now: 3.0.0 bound at acceptance; bound today to 3.1.0 because every Returns operation and `getShipment` are identical (Q218) |
| MOD-0186 `:619` §32.3 "Published SHIPMENT-BUNDLE 3.0.0 / wire v1 (`5dfe7c1b…`)" | effective binding of the UI scope | now 3.1.0 (`6dc1dd48…96aa2`; accepted on 3.0.0, see §31) |
| MOD-0186 `:786` gap "Loads 3.1.0 forward drift of the `5dfe7c1b…` pin" | open gap | marked "closed by the Q380 re-pin to 3.1.0" |
| MOD-0187 `:10`, `:533`, `:535`, `:569`, `:735` | as MOD-0186 `:10`, `:584`, `:586`, `:619`, `:786` | same actions; the WHY names `getShipment` and `queryCarriers` |
| MOD-0187 `:501` §29 "Proposed final target 3.0.0" | history (§31 calls §29 historical) | **kept** `5dfe7c1b` |

**This departs from the dispatch's "re-pin every occurrence"** for the history lines MOD-0186 `:484`, `:564` and
MOD-0187 `:501`. Rewriting them would make the packs claim that acceptance evidence was produced on bytes it was not
produced on. The authority order in the dispatch's own contract puts Q218 first, so Q218's rule was applied. CT can
overrule this; the change would be three digests in three history lines.

## Proof

| pack | 5dfe7c1b before → after | 6dc1dd48 before → after | where 5dfe7c1b remains |
|---|---|---|---|
| MOD-0186 | 5 → 3 | 0 → 3 | `:484` (§28 history), `:564` (§30 history), `:584` (§31 "at acceptance") |
| MOD-0187 | 4 → 2 | 0 → 3 | `:501` (§29 history), `:533` (§31 "at acceptance") |

Recomputed digest of the canonical file: `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`
(`evidence/digests.txt`). Counts: `evidence/counts.tsv`. Exact edits: `evidence/repin.diff`.

**No normative text changed beyond the pin.**
- MOD-0186: +6 / −5 lines (hunks `10c10`, `584c584,585`, `586c587`, `619c620`, `786c787`).
- MOD-0187: +6 / −5 (hunks `10c10`, `533c533,534`, `535c536`, `569c570`, `735c736`).

Every changed line carries the version or digest, or the reason for it. No operation, field, rule, acceptance row,
annex or owned path was added, removed or altered. §32.3's list of bound operations is word-for-word unchanged apart
from the version and digest in its lead-in.

Structure (before → after, each pack): headings 55 → 55 (identical list) · tables 21 → 21 (MOD-0186), 19 → 19
(MOD-0187) · bad table rows 0 → 0 · code fences balanced · lines +1 each (the split §31 row).

Pack sha256: MOD-0186 `aeaeecfb…` → `be669789…`; MOD-0187 `e5cbe101…` → `7a89b0d3…`. Pre-edit copies:
`~/mvp6-env/q380-20261004-1230/`.

## Findings

- **F-Q380-1.** The Q218 premise was re-measured and holds (`evidence/compare-ops.txt`).
- **F-Q380-2.** The dispatch's "every occurrence" conflicts with Q218's "the acceptance value should stay visible". Q218 was followed, and three history lines keep `5dfe7c1b` (table above).
- **F-Q380-3.** MOD-0183 pins by path and wire `v1` only (`:134`, `:77`, `:151`). It needs no re-pin, and it cannot drift the same way.
- **F-Q380-4.** Q218's related point stands: **MOD-0185's effective pin is 2.0.0 `93c696e2…`** (Q218 `PIN-INVENTORY.tsv`). It is further behind than these two packs, and it is outside this lane.
- **F-Q380-5.** The 3.0.0 bytes exist only in the 2026-09 record folder named above. That folder is the only way anyone can check the `5dfe7c1b` acceptance pins that remain.
- **F-Q380-6.** The re-pin closes the literal "versions match" reading of SOP §27.1 for these two packs. It does not add run-time evidence: Q218 noted that neither module had called the real Shipment endpoint in that tree.

Nothing committed, nothing pushed, nothing staged.

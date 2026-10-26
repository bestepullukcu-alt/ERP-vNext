# MVP6 development plan v9.5 — 2026-10-13

Supersedes v9.4 (2026-10-12). Everything here is measured; where a number moved, the lane that moved it is
named so the claim can be checked rather than believed.

## 1. What v9.4 got wrong, and what CT got wrong under it

**v9.4 was right and CT did not read it.** Its §3 already recorded MOD-0185 row 1 as *"closed by code
10-06 … done"* and rows 10 for MOD-0186/0187 as *"CLOSED UNDER WAIVER 10-05, not NOT MET … done"*. CT opened
a lane on row 1 from `GATE-18-0.tsv` without re-reading the newer plan, re-derived a settled conclusion, and
separately told the owner that rows 10 were still open. Both corrected in
`mvp6-stage-r-row1-loads-config-01/REPORT.md`. This is the v9.3 mistake repeating: acting from an older
artefact while a newer one in the tree had already settled it.

The guard that lane produced is still new work, and so is the near miss it found — see §4.

**Two findings CT started to write and withdrew on measurement**, recorded because the withdrawal is the
useful half: a supposed localization defect in MOD-0184 (the counting method was wrong — `grep -c` counts
lines, and a `find | head -1` picked CRM's unrelated `ClaimsIndex` set; all four modules carry seven
languages with equal counts), and a supposed contract violation on `GET /shipments` (it was R-2's
fail-closed legal-entity validator refusing an unseeded scope, and the `SHIPMENT_NOT_FOUND` code it refuses
with is a deliberate non-disclosure choice, not a misattribution).

## 2. Measured state — the gate, 56 cells

| | v9.4 | v9.5 | moved by |
|---|---:|---:|---|
| MET | 37 | **43** | six cells, today |
| NOT MET | 9 | 4 | |
| NOT MEASURED | 10 | 9 | |

Today's six, each with its own sealed lane and `MOVED.tsv`:

| cell | from | to | lane |
|---|---|---|---|
| MOD-0184 row 13 Observability | NOT MET | MET | `mvp6-stage-r-row13-01` |
| MOD-0185 row 13 Observability | NOT MET | MET | `mvp6-stage-r-row13-01` |
| MOD-0186 row 13 Observability | NOT MET | MET | `mvp6-stage-r-row13-02-returns-claims` |
| MOD-0187 row 13 Observability | NOT MET | MET | `mvp6-stage-r-row13-02-returns-claims` |
| MOD-0186 row 5 Concurrency | NOT MEASURED | MET | `mvp6-stage-r-row5-returns-01` |
| MOD-0185 row 1 Golden flow | NOT MET | MET | Q457 on 10-06; this lane only re-measured and guarded it |

**Row 13 is now MET for all four modules.** It needed no new code: the Q446 notes pointed at the context
middlewares, but the replay signal is logged by the **controllers**, which already carried it
(`LoadsController.cs:21`, `CarriersController.cs:27`, `ReturnsController.cs:26`, `ClaimsController.cs:28`).
MOD-0184's `:376` reservation never blocked it — it withholds runtime *write*, not observation.

`GATE-18-0.tsv` itself was **not edited**: it sits inside its own lane's `ARTIFACTS.sha256` and editing it
would break a dated audit seal. Each movement is in a `MOVED.tsv` that supersedes those cells (Q487).

## 3. The four NOT MET, and two of them are already closed

| cells | state |
|---|---|
| rows 3, MOD-0186 / MOD-0187 | **integration owner.** The packs declare explicit per-module routes while the running gateway serves one catch-all. Measured today: the contradiction is **documentary** — the same request answers identically direct at 5066 and through the gateway on all five paths, so nothing is swallowed or misrouted (Q497). `gateway/**` is Protected Paths in every pack |
| rows 10, MOD-0186 / MOD-0187 | **already waived** 2026-10-05 (`mvp6-returns-claims-evidence-row10-waiver-owner-decision-01.md`). The TSV is stale, the plan is not |

## 4. The nine NOT MEASURED — all browser, all blocked on one authorization

Rows 2 (×4), 7 (×2) and 12 (×3). Rows 7 and 12's **server** halves are already evidenced in the suites; only
the browser halves are open.

Everything in front of them is now proven, which is this session's real gain:

- **The stack runs.** Six services, a loopback credential helper and **one** lane mongod, all measured
  listening (`mvp6-stage-r-stack-up-01`). One mongod serves all four data-owning services because their
  database names do not collide, saving about a gigabyte against the R-2 lane's two. Five service builds cost
  about 200 MB in total, so v9.4's disk worry was overstated.
- **Sign-in works and leaked nothing.** The R-2 lane's own method: a loopback helper served an
  `openssl`-generated credential from a mode-600 file, so no password passed through the transcript.
- **Routing, shell and the denied state are confirmed live on all four modules.** Each route is served, each
  renders its own message inside the tenant shell, and the URL does not change — UAS-001, observed rather
  than cited (`mvp6-ui-pack-wave-q322-01/LIVE-DENIED-ACCESS.tsv`).
- **Row 2 is viable.** Save View and column visibility are implemented end to end, not a label: the chain
  from `_IndexL10n` through `index.js`, `personalization-client.js`, `PersonalizationProxyController` and
  `ocelot.json` to Platform's `SavedViewsController.cs:13` is measured, and the saved view is server state.

**The block:** the lane account is role `Viewer` and holds none of the 25 `supplychain.*` keys, so every
module page renders denied. Closing the nine needs one write into this lane's own disposable auth database.
CT could not run it — the harness refused with reason `[Permission Grant]`, and refused a second command that
merely counted those keys, so the refusal is on the subject matter. **This is the single thing standing
between today's state and nine cells.**

## 5. Owner queue

| # | decision | state |
|---|---|---|
| 1 | **Q484** — the Capacity context middleware branch in `Program.cs` | CT refuses the re-pin the release included, because Q292 (owner-approved 10-03) keeps that hash and Q298 executed it *"without altering its hash"*. CT recommends the **test-host** option: `SandopCapacityHttpHost`'s own header records that the owner read Q292 on 10-11 as **not binding a test host**. Measured today: the production container is intact with the branch in place, `CompositionRootGuardTests` 3/3 |
| 2 | **Q496** — one write to give the lane account the 25 module keys | unblocks nine cells; stack left running so it needs no rebuild |
| 3 | **Q322** — the four UI pack revisions | proposed and sealed in `mvp6-ui-pack-wave-q322-01`; needs owner approval per pack. All four measured **Slim** (field counts 5/8/6/7 against AGENTS.md:185 `8 ve altı`), and **MOD-0185 sits exactly on the boundary** — one more field forces the Compact four-file restructure |
| 4 | Rows 3 integration-owner release (gateway explicit routes) | decided 10-05, sequence gate open since 10-10, nobody assigned |
| 5 | **Q493** — two personalization key conventions across four modules; `ASSUMPTION A6` asked the integration owner to confirm them and nobody did | does not block row 2 |
| 6 | **Q498** — `GET /shipments` under-declares its responses (200/400/503 while siblings declare seven, and the middleware returns 401/403/404/503 on that path); `GET /claims` omits 404 | contract edit, inherited from 3.2.0 |
| 7 | **MOD-0184 runtime-write release** (`:376`) | unchanged |
| 8 | **Q15** integration execution in one registered checkout | unchanged, still the Stage 2 gate |

## 6. Two configuration defects of the same family as Q414

Q414 cost MOD-0185 a golden-flow row because a service worked only when a lane supplied by hand what the tree
should carry. Two more of that family, found by actually starting the stack:

1. **`JwtSettings` was missing from SupplyChain's tracked configuration** — `Program.cs:46` throws without
   it, so the service could not boot from a clone, and a token minted by Auth could not have validated there.
   Its three siblings all carry the same dev values under the owner-decided pattern (`c74ea4839`). Fixed,
   +5 lines, digest matching the siblings; no value in any record (Q495).
2. **The tracked configuration had no test at all.** Measured: nothing in the repository read
   `appsettings.json` or `appsettings.Development.json`. Q457's owner-applied fix had already drifted once —
   recorded as `5061`, now correctly `5066` after OD-P1 renumbered SupplyChain and gave 5061 to **CRM** — and
   nothing would have caught it. `TrackedConfigurationGuardTests` now asserts the two relationships that rot
   silently, with three sabotages that fire precisely (Q491).

Not fixed, and deliberately: `Mongo:DatabaseName` has **never** been tracked (`git log -S` finds nothing) and
every lane supplies its own. Two keys, two different answers, measured separately.

## 7. Not MVP-6, but delivered from it today

SHIPMENT-BUNDLE **3.3.0** for MOD-0188 demand history is pushed on
`contracts/shipment-bundle-3.3-demand-history` (`4b4722ece`), additive only, PR left for the owner. The
realized fields went on a **response-only** schema rather than `ShipmentLine`, because that schema is shared
with `CreateShipmentCommand.lines` and carries `additionalProperties: false` — adding there would have made
server-derived, post-dispatch-immutable values writable on the create request. Verified node by node:
nothing removed, 12 paths before and after, 47 schemas to 51, 319 `$ref`s all resolving.

## 8. Measures

| | |
|---|---|
| gate cells MET | **43 of 56** |
| cells closed today | 6 |
| cells blocked on one authorization | 9 |
| cells with the integration owner | 2 |
| sealed lanes today | 10 |
| queue rows added today | 15 (Q484-Q498) |
| commits on the MVP-6 branch | 1 (`baa35a7bf`, the `:202` evidence), nothing since |

# Q292 §22 composition gate — OPENED for MOD-0190 / MOD-0192 (fixture-demand interim)

> **OWNER DECISION.** Opens the §22 composition gate both packs reserve over `Program.cs`, and selects the
> **composed-but-fixture-demand interim** demand path. Authorises composition + the re-pin; the real-demand
> swap is deferred to when DEMAND v2 freezes (G3). Request: `mvp6-0190-0192-ui-wave-01/G1-SECTION22-GATE-OPEN-REQUEST.md`.

- owner: **ny@gmgroup.ch** (integration-owner / §22 authority) — relayed by the Control Tower, 2026-10-18
- choice: **Approve G1 + fixture-demand interim**

## Authorised

1. Compose MOD-0190 S&OP and MOD-0192 Capacity in `Program.cs` **against the existing test fixtures**
   (`DemandFixtureReader`, `ConstraintFixtureReader`) — a real composed endpoint, fixture data.
2. Add the Capacity pipeline branch and **un-gate** the Capacity middleware from Testing-only (reverse
   Q484's env-gate) so it runs in production.
3. **Re-pin** both packs' Accepted-composition hashes to the new `Program.cs` (MOD-0190 `:261`
   `a2a216be…`, MOD-0192 `:273` `50c48a2b…`; current file is `7513971f…`).

## Deferred (not this decision)

- The real-demand swap (`DemandV2HttpReader`, G3) — waits for the MOD-0188 owner to freeze DEMAND v2 and
  the consumer re-model in `G3-DEMAND-V2-CONSUMER-ADAPTER-DESIGN.md`. The interim uses fixtures until then.
- SUPPLY-CONSTRAINTS live adapter (G2) — Capacity composes against `ConstraintFixtureReader` in the interim;
  the live constraint client waits for the G2 contract freeze + a named producer owner.
- Gateway route activation (G5), permissions incl. `demand.plans.consume` (G7), UI build (G6), integration
  + VER (G8) — their own wave steps.

## Execution

CT prepared the composition **spec** (`mvp6-0190-0192-ui-wave-01/G4-COMPOSITION-SPEC.md`) — the clean changes
exact, plus three items that need a build to resolve (fixture seed values, the /sandop catch-all question,
and the missing 0190/0192 manifest providers). The **integration-agent** applies it and re-pins both packs —
`Program.cs` is the §22 integration-owner surface, and CT editing it without this gate is the reverted
mistake (`MOD-0192 :199`). CT does not apply it.

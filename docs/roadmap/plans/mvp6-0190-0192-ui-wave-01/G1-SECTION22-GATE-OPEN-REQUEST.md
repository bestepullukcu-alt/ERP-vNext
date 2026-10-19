# G1 — Q292 §22 composition gate-open request (MOD-0190 S&OP + MOD-0192 Capacity)

    to        : the integration-owner / §22 gate authority (the owner, ny@gmgroup.ch)
    from      : Control Tower (gmg-10) · 2026-10-18
    basis     : the full-working-UI override
                (docs/records/decisions/2026-10/mvp6-0190-0192-ui-override-owner-decision-01.md) and the
                wave plan (mvp6-0190-0192-ui-wave-01/PLAN.md, gate G1).
    asks      : OPEN the §22 composition gate for 0190/0192 — i.e. authorise composing them in
                `Program.cs` and re-pinning both packs' Accepted-composition hashes. This is the
                AUTHORISATION; the actual compose (G4) is still sequenced behind G2 (SUPPLY-CONSTRAINTS)
                and G3 (DEMAND v2), or the fixture-demand interim.

## What §22 reserves today (both packs, verbatim intent)

Both packs pin `Program.cs` as "one separately authorized integration-owner surface" and keep
common-checkout integration, gateway/shared registration, live producers, UI, E5/G5 and rollout **behind
the §22 gate** (MOD-0190 `:23,:99,:257,:261`; MOD-0192 `:226,:257,:267,:273`). Until §22 opens, 0190/0192
are not composed in production — S&OP/Capacity modules are unregistered and every `/api/supply-chain/...`
path 404s (Capacity's own `:199/:200` lines are blocked on exactly this).

## What OPENING §22 authorises

1. **Register MOD-0190 and MOD-0192** in `Program.cs` (manifest providers + controllers), as the five
   shipment-bundle modules already are.
2. **Add the Capacity pipeline branch** so `CapacityContextMiddleware` runs for `/api/supply-chain/capacity-*`
   instead of falling into the Shipment catch-all (the exact, already-measured `:199` fix CT previously
   wrote and **reverted for lack of §22 authority** — this request is that missing authority).
3. **Un-gate the Capacity middleware from Testing-only** (reverse Q484's `app.Environment == "Testing"`
   env-gate) so it is composed in production too.
4. **Re-pin both packs' Accepted-composition hashes** to the new `Program.cs`.

## Hash reality (so the re-pin is explicit)

| pin | value | state |
|---|---|---|
| MOD-0190 `:261` Accepted composition | `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5` | **stale** |
| MOD-0192 `:273` Accepted composition | `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0` | **stale** |
| current `Program.cs` (HEAD) | `7513971f3b314e07a5f098f2348bb78e9a6c97256d0383b1a3365011e2f7d4b4` | live |

The current file **already differs from both pins** — Q484 (owner, 2026-10-18) added the Testing-only
Capacity middleware line (`Program.cs:153`). So a re-pin is required regardless; opening §22 makes it an
authorised re-pin rather than a broken pin. (CT's earlier unauthorised edit moved the file to `db4ad0de…`
and broke the pins; it was restored. This gate prevents a repeat.)

## Scope of this gate-open (what it does NOT cover)

§22-open here authorises **Program.cs composition + the re-pin only**. It does not itself:
- compose anything today — G4 still waits for G2 (SUPPLY-CONSTRAINTS contract, draft at
  `supply-constraints.openapi.draft.yaml`) and G3 (DEMAND v2 uptake, cross-team / MOD-0188), unless the
  owner takes the **composed-but-fixture-demand interim** (compose against the existing fixtures now, swap
  to real demand when v2 freezes);
- activate the 10 reserved gateway routes (G5), register permissions incl. `demand.plans.consume` (G7), or
  build the UI (G6) — each is its own step in the wave plan (the override authorises them program-wide;
  this request is only the §22 composition gate).

## Execution after APPROVE (integration-agent; `Program.cs` is Protected)

CT prepares the exact composition diff on approval; the **integration-agent applies it** (the `:199`
measurement makes it a short, known change) and re-pins both packs. CT does **not** edit `Program.cs` — that
is the integration-owner surface, and editing it without this gate is exactly the reverted mistake.

## Owner response (choose one)

```
APPROVE §22 gate-open for 0190/0192  — authorise Program.cs composition + re-pin; compose via [G2+G3] | [fixture-demand interim]
```
or
```
REJECT — keep §22 closed; 0190/0192 stay uncomposed
```

## Files

- `G1-SECTION22-GATE-OPEN-REQUEST.md` — this. Next on APPROVE: CT prepares the composition diff; the wave
  plan's G4 proceeds on the chosen demand path.

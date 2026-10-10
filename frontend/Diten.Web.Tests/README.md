# Diten.Web.Tests — what a green run does and does not prove

Read this before quoting the suite's pass count as frontend coverage.

## The suite runs no browser and no DOM

The project references xunit, `Microsoft.AspNetCore.Mvc.Testing` and the test SDK. It has **no JavaScript engine of
its own**. Three kinds of test live here:

| kind | what it executes | example |
|---|---|---|
| Server-side (controllers, views, resources) | real C# through `Mvc.Testing` | `Controllers/`, `Auth/`, `Navigation/` |
| **Source-text checks** on `.js` files | nothing — the file is read with `File.ReadAllText` and its **text** is asserted | `JavaScript/ShipmentDetailActionTests.cs`, `JavaScript/ShipmentIndexBehaviorTests.cs` |
| **Node behaviour tests** (Q382) | the shipped `.js` file, under Node, at fixed time zones | `JavaScript/ShipmentDetailsDateNodeTests.cs` → `JavaScript/Node/*.test.mjs` |

A source-text check cannot tell right code from wrong code that contains the same words. It can even pin a defect:
asserting that `toISOString().slice(0, 16)` is present would have pinned the Q374 time-zone bug, not caught it.

## The gap the owner accepted (decision 2026-10-04 §2)

**DOM behaviour — what a page draws, what a click or submit does, which field gets focus — is proven only by live lane
runs that drive a real browser, never by this suite.** Every frontend behaviour proven so far was proven that way.

The Node tests cover pure functions only, and only where a function has been made reachable: today the two Shipment
Details date conversions (`localInputValue`, `parseLocalInput`), at `TZ=Europe/Istanbul` and `TZ=UTC`. Both zones
are required: a UTC-only run stays green with the Q374 defect present.

So a figure like "161/1/162" means: the server side and the date conversions passed, and the rest of the frontend's
behaviour was **not tested by this run**.

## Running

- `dotnet test frontend/Diten.Web.Tests` runs everything, including the Node tests (one case per zone). It needs
  `node` (18 or later) on the PATH; without it those two cases fail, they do not skip. CI runs this command
  (`scripts/run_phase1_gates.sh`).
- The Node tests alone, from the repository root:
  `TZ=Europe/Istanbul node --test frontend/Diten.Web.Tests/JavaScript/Node/shipment-details-datetime.test.mjs`, then
  the same with `TZ=UTC`. Name the file: since Node 21 a bare directory argument fails. Without one of those two `TZ`
  values the zone test fails on purpose.

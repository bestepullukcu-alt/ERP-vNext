# The Claims restart test: throw → allow-listed skip

CT can edit `services/**` (it is `ask`, not `deny`), but these two edits are **in this package rather
than committed** so the change lands as one unit. On its own it would alter the suite's shape — 529
passed + 1 skipped instead of the 529/1/530 the merge-completion gate recorded — for a CI row that
cannot land yet.

## A. new file — `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimsRestartModeFact.cs`

```csharp
using Xunit;

namespace Diten.SupplyChainService.Tests.Claims;

/// <summary>
/// WP-LANE-MAIN-SYNC-05 — the two-process restart test is not runnable in an ordinary suite, and said so by throwing:
/// "Explicit write/read restart mode required; exclude from ordinary suite." A throw is a FAILURE to any runner, so the
/// supplychain-tests CI row could never be green while it existed.
///
/// This expresses the author's own intent in the language the harness understands: a Skip whose reason STARTS with a
/// code, which scripts/ci-skip-allowlist.tsv matches and caps. Same mechanism as
/// AuthService's UnixConditionalFacts (MACOS_ONLY, UID_EQUALS_GID, RUNS_AS_ROOT) and the same shape as
/// manufacturing-tests' MANUFACTURING_TEST_MONGO allowance.
///
/// It does NOT weaken the test: with CLAIMS_RESTART_MODE set to write or read the attribute is an ordinary [Fact] and
/// the body runs unchanged, including its own parse of the mode and of CLAIMS_RESTART_TENANT.
/// </summary>
public sealed class ClaimsRestartModeFactAttribute : FactAttribute
{
    internal const string ModeVariable = "CLAIMS_RESTART_MODE";

    public ClaimsRestartModeFactAttribute()
    {
        var mode = Environment.GetEnvironmentVariable(ModeVariable);
        if (mode is not ("write" or "read"))
        {
            Skip = $"{ModeVariable}: a two-process restart test; set {ModeVariable} to write or read "
                 + "together with CLAIMS_RESTART_TENANT to run it. Excluded from the ordinary suite by design.";
        }
    }
}
```

## B. one line in `Claims/ClaimReplayTests.cs`

Replace the attribute on line 203:

```csharp
    [Fact]
    public async Task Receipt_TwoIndependentTestProcesses_DurableRecovery()
```

with:

```csharp
    [ClaimsRestartModeFact]
    public async Task Receipt_TwoIndependentTestProcesses_DurableRecovery()
```

**Leave the body's guard exactly as it is** (lines 205–206):

```csharp
        var mode=Environment.GetEnvironmentVariable("CLAIMS_RESTART_MODE");
        if(mode is not ("write" or "read"))throw new InvalidOperationException("Explicit write/read restart mode required; exclude from ordinary suite.");
```

It is now unreachable through the attribute, and that is on purpose: if someone removes the attribute
the test goes back to failing loudly instead of silently passing with nothing checked. A guard whose
removal is invisible is not a guard.

## What this is measured against

Before: the full suite is **529 passed, 1 failed, 0 skipped of 530** — the one failure is this test.
After: **529 passed, 0 failed, 1 skipped**, and the skip is the single allow-listed
`CLAIMS_RESTART_MODE` with `max 1`, so `scripts/check_trx_no_skipped.py` passes.

CT did **not** run this: the attribute file is unwritten because the change is held as a unit. The
assertion above is arithmetic on a measured run, not an executed result — the first local run after
applying files 4 and 5 is the measurement.

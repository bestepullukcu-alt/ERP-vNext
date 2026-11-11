using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Contract;

/// <summary>
/// MOD-0175-FU01 DL-3 AC-36 (pack §8.8; Q-06 = A, Q-07) — INVENTORY 1.8.0's one new refusal, <c>REVERSAL_QUALITY_RECORD_LOCKED</c>, is declared
/// in the contract (an example under the existing 422 of postMovement, the postMovement description, the 1.8.0 changelog line) AND is in the
/// service's closed catalogue <see cref="InventoryErrorCodes.All"/>; the closed <c>details.cause</c> list of the contract is the guard's.
/// Sabotage: the code taken out of <see cref="InventoryErrorCodes.All"/> → red.
/// </summary>
public sealed class InventoryBundle180ContractTests
{
    [Fact]
    public void AC36_the_new_code_is_in_the_contract_and_in_the_closed_catalogue()
    {
        var text = File.ReadAllText(ContractSchemas.ContractPath);
        Assert.Contains("version: 1.8.0", text, StringComparison.Ordinal);
        Assert.Contains("code: REVERSAL_QUALITY_RECORD_LOCKED,", text, StringComparison.Ordinal);
        Assert.Contains(InventoryErrorCodes.ReversalQualityRecordLocked, InventoryErrorCodes.All);
        Assert.Equal("REVERSAL_QUALITY_RECORD_LOCKED", InventoryErrorCodes.ReversalQualityRecordLocked);
    }

    [Fact]
    public void AC36_the_closed_cause_list_of_the_contract_is_the_guards()
    {
        string[] causes =
        [
            QuarantineReversalPrecondition.OwnMovementReversed, QuarantineReversalPrecondition.Decided, QuarantineReversalPrecondition.LinesPosted,
            QuarantineReversalPrecondition.QuantityChanged, QuarantineReversalPrecondition.DecisionInProgress, QuarantineReversalPrecondition.Unmatched
        ];
        var text = File.ReadAllText(ContractSchemas.ContractPath).Replace("\r\n", "\n");
        Assert.Contains("details.cause is one of " + string.Join(", ", causes) + ";", text.Replace("\n        ", " "), StringComparison.Ordinal);
        Assert.Contains("(" + QuarantineReversalPrecondition.HoldType + " | " + QuarantineReversalPrecondition.BlockedType + ")", text, StringComparison.Ordinal);
    }
}

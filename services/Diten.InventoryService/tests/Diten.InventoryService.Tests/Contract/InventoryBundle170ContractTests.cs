using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Contract;

/// <summary>
/// MOD-0173-FU03 AC-30 (§8.8, CT-02) — the five reservation refusals of INVENTORY 1.7.0 are declared in the contract (examples under the
/// existing 422 of postMovement) AND are in the service's closed catalogue <see cref="InventoryErrorCodes.All"/>: neither side may hold a
/// code the other does not.
/// </summary>
public sealed class InventoryBundle170ContractTests
{
    private static readonly string[] NewCodes =
        ["RESERVATION_NOT_FOUND", "RESERVATION_NOT_ACTIVE", "RESERVATION_MISMATCH", "RESERVATION_NOT_APPLICABLE", "RESERVATION_QUANTITY_EXCEEDED"];

    /// <summary>S-21: one of the five codes taken out of InventoryErrorCodes.All → red.</summary>
    [Fact]
    public void New_codes_in_openapi_and_catalog()
    {
        var text = File.ReadAllText(ContractSchemas.ContractPath);
        Assert.All(NewCodes, code => Assert.Contains("code: " + code + ",", text, StringComparison.Ordinal));
        Assert.All(NewCodes, code => Assert.Contains(code, InventoryErrorCodes.All));
        Assert.Equal(NewCodes.OrderBy(c => c, StringComparer.Ordinal),
            InventoryErrorCodes.All.Where(c => c.StartsWith("RESERVATION_", StringComparison.Ordinal) && c != InventoryErrorCodes.ReservationConflict)
                .OrderBy(c => c, StringComparer.Ordinal));
    }
}

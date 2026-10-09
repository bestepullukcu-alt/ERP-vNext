using System.Reflection;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;
using Diten.MdmService.Tests.Shared;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// MOD-0290-FU04 S3b FIX1 (R4) — every identity submit of a stock-bearing level asks the stock-flag gate. Measured on the
/// compiled handlers (their own IL, async parts and lambdas included — not a helper they happen to call), so a renamed or new <c>Start*IdentityWorkflowHandler</c> — the
/// Finished Good's, when it comes — is held by the same rule without a list to update. The Global Product is the one
/// level with no stock flags; it is named here, and only it.
/// </summary>
public sealed class StockFlagGateCoverageTests
{
    private static readonly string[] LevelsWithoutStockFlags = ["StartGlobalProductIdentityWorkflowHandler"];

    [Fact]
    public void S3b_FIX1_Every_identity_submit_handler_of_a_stock_level_asks_the_stock_flag_gate()
    {
        var handlers = typeof(StartLskuIdentityWorkflowHandler).Assembly.GetTypes()
            .Where(x => x is { IsClass: true, IsAbstract: false } && x.DeclaringType is null
                        && x.Name.StartsWith("Start", StringComparison.Ordinal)
                        && x.Name.EndsWith("IdentityWorkflowHandler", StringComparison.Ordinal))
            .ToArray();
        var gate = typeof(StockFlagGate).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.Name.StartsWith("Missing", StringComparison.Ordinal)).ToHashSet<MemberInfo>();

        var stockLevels = handlers.Where(x => !LevelsWithoutStockFlags.Contains(x.Name)).ToArray();
        Assert.Contains(typeof(StartFirstGskuIdentityWorkflowHandler), stockLevels);
        Assert.Contains(typeof(StartLskuIdentityWorkflowHandler), stockLevels);
        Assert.All(LevelsWithoutStockFlags, name => Assert.Contains(handlers, x => x.Name == name));

        var silent = stockLevels.Where(x => !IlReferences.MembersReachedFrom(x, transitive: false).Any(gate.Contains)).Select(x => x.Name).ToArray();
        Assert.Empty(silent);
    }
}

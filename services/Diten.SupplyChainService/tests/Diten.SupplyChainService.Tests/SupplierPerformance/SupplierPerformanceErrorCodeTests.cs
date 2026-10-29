using System.Reflection;
using System.Text.RegularExpressions;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Tests.Shipments;
using Xunit;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>
/// Every code MOD-0147 emits is declared somewhere with authority: by the frozen SUPPLIER-PERFORMANCE v1 contract, or by the
/// pack (§0.0 undeclared statuses, §0.5) for the contract successor 1.1.0. 401 and 500 are declared by the pack without a code;
/// they carry the house codes of <see cref="HouseCodes"/>. Both files are read from the repository on every run.
/// </summary>
public sealed class SupplierPerformanceErrorCodeTests
{
    private static readonly string[] HouseCodes = ["UNAUTHENTICATED", "INTERNAL_ERROR"];

    private static string[] Constants() => typeof(SupplierPerformanceErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).ToArray();

    [Fact]
    public void EveryEmittedCode_IsDeclaredByTheContractOrThePack()
    {
        var pack = File.ReadAllText(RepositoryFile.Locate("execution/domains/supply-chain-execution/module-packs/MOD-0147-supplier-performance-risk.md"));
        // The pack names its frozen contract (§0 "ONAYLI" row); the test follows that pointer instead of repeating the path.
        var contractPath = Regex.Match(pack, @"`(?<path>[^`]*/supplier-performance\.openapi\.yaml)`").Groups["path"].Value;
        Assert.NotEmpty(contractPath);
        var contract = Regex.Matches(File.ReadAllText(RepositoryFile.Locate(contractPath)), @"\bcode:\s*([A-Z][A-Z0-9_]*)\b")
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        var undeclared = Constants().Where(c => !contract.Contains(c) && !pack.Contains("`" + c + "`", StringComparison.Ordinal) && !HouseCodes.Contains(c)).ToArray();

        Assert.True(undeclared.Length == 0, "Emitted but declared by neither the contract nor the pack: " + string.Join(", ", undeclared));
        Assert.Subset(contract, new HashSet<string>(["INVALID_REQUEST", "UNKNOWN_SUPPLIER", "UNKNOWN_EVALUATION", "UNKNOWN_SCORECARD", "BUSINESS_RULE_VIOLATION", "SUPPLIER_BASE_UNAVAILABLE"]));
    }
}

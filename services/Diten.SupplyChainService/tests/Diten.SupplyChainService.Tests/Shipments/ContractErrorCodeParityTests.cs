using System.Reflection;
using System.Text.RegularExpressions;
using Diten.SupplyChainService.Application.Common;
using Xunit;

namespace Diten.SupplyChainService.Tests.Shipments;

// Q288 group 1 (closes Q262's open half): ContractErrorCodes must equal the codes the SHIPMENT-BUNDLE contract
// declares, in both directions. The contract is read from the repository on every run, never copied in here.
public sealed class ContractErrorCodeParityTests
{
    private const string ContractPath = "docs/analysis/contracts/shipment-bundle.openapi.yaml";

    // The extraction rule ContractErrorCodes documents: every `code: UPPER_CASE` occurrence in the contract.
    private static HashSet<string> ContractCodes() =>
        Regex.Matches(File.ReadAllText(RepositoryFile.Locate(ContractPath)), @"\bcode:\s*([A-Z][A-Z0-9_]*)\b")
            .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ConstantCodes() =>
        typeof(ContractErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void ContractErrorCodes_WhenComparedWithContract_ContainsEveryContractCode()
    {
        var missing = ContractCodes().Except(ConstantCodes()).OrderBy(code => code, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0, "In the contract but not in ContractErrorCodes: " + string.Join(", ", missing));
    }

    [Fact]
    public void ContractErrorCodes_WhenComparedWithContract_DeclaresNoCodeTheContractLacks()
    {
        var extra = ConstantCodes().Except(ContractCodes()).OrderBy(code => code, StringComparer.Ordinal).ToArray();

        Assert.True(extra.Length == 0, "In ContractErrorCodes but not in the contract: " + string.Join(", ", extra));
    }

    [Fact]
    public void ContractErrorCodesAll_WhenComparedWithConstants_ListsEachConstantExactlyOnce()
    {
        var all = ContractErrorCodes.All;

        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ConstantCodes().OrderBy(code => code, StringComparer.Ordinal), all.OrderBy(code => code, StringComparer.Ordinal));
        Assert.NotEmpty(all);
    }
}

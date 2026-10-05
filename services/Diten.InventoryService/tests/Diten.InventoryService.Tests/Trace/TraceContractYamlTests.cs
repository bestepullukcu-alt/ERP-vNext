using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// WP-0174-P1 — the TRACE v1.1 single-writer step is ADDITIVE ONLY (proposal §9 check table). The frozen v1.0.0 file is
/// pinned in <c>Contract/Fixtures/trace-v1.0.0.openapi.yaml</c> (SHA-256 of the git blob at 7bcbfca53, LF-normalised) and
/// every v1 element must be present and equal in <c>trace.openapi.yaml</c> 1.1.0: servers, security, every path /
/// method / parameter / request body / response code / response body, every schema and every property of it (type,
/// format, nullable, enum, required). The ONLY v1 texts allowed to change are the three B-1 descriptions (business key
/// Tenant+Item+LotNumber → Tenant+SKU+LotNumber, owner change of DEC-INV-14), each named below.
/// </summary>
public sealed class TraceContractYamlTests
{
    private const string FrozenV1Sha256 = "2fa5e20b8de84deba16533c24bc9e1a090847c9494d73a1752522e1f69ea2297";

    /// <summary>The v1 texts B-1 rewrites (accepted proposal §8) — nothing else of v1 may differ.</summary>
    private static readonly HashSet<string> AllowedChangedDescriptions = new(StringComparer.Ordinal)
    {
        "info.version", // 1.0.0 → 1.1.0 (asserted exactly by Version_and_changelog_…)
        "info.description",
        "components.schemas.LotRef.properties.lotNumber.description",
        "components.responses.LotConflict.description"
    };

    private static string FixturePath => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "trace-v1.0.0.openapi.yaml");

    [Fact]
    public void The_v1_fixture_is_the_frozen_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixturePath).Replace("\r\n", "\n");
        Assert.Equal(FrozenV1Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    [Fact]
    public void Version_and_changelog_name_1_1_0_and_the_three_behaviour_changes()
    {
        var current = Load(ContractSchemas.TraceContractPath);
        var info = Map(current["info"]);
        Assert.Equal("1.1.0", info["version"]);
        Assert.Equal("FROZEN", info["x-status"]);
        Assert.Equal("MOD-0174", info["x-owner"]);
        var changelog = string.Join("\n", List(info["x-changelog"]).Cast<string>());
        Assert.Contains("1.0.0", changelog);
        Assert.Contains("1.1.0", changelog);
        foreach (var marker in new[] { "B-1", "B-2", "B-3", "LOT_SKU_REQUIRED" }) Assert.Contains(marker, changelog);
    }

    [Fact]
    public void Every_v1_element_is_present_and_unchanged_in_1_1_0()
    {
        var v1 = Load(FixturePath);
        var current = Load(ContractSchemas.TraceContractPath);
        var differences = new List<string>();
        Contained(v1, current, "", differences);
        Assert.True(differences.Count == 0, "v1 elements removed or changed:\n" + string.Join("\n", differences));
    }

    [Fact]
    public void The_accepted_v1_1_operations_are_all_declared()
    {
        var current = Load(ContractSchemas.TraceContractPath);
        var operations = Map(current["paths"]).Values.Cast<object>()
            .SelectMany(p => Map(p).Values.OfType<IDictionary>().Select(o => o["operationId"] as string))
            .ToHashSet();
        string[] expected =
        [
            "getLot", "listLots", "createLot", "getSerial", "getGenealogy", "validateTrace",
            "updateLot", "changeLotStatus", "getLotHistory", "getLotDetail", "listSerials", "checkGs1Code", "linkGenealogy",
            "listManualLotGroups", "commitManualLotMigration", "getMovementLotLinks"
        ];
        Assert.All(expected, op => Assert.Contains(op, operations));
    }

    /// <summary>
    /// Every key / item of <paramref name="v1"/> must exist in <paramref name="current"/> with an equal scalar. Maps may
    /// GAIN keys (additive); lists must keep their v1 items (a parameter list may gain items, a required / enum list may
    /// not change).
    /// </summary>
    private static void Contained(object? v1, object? current, string path, List<string> differences)
    {
        switch (v1)
        {
            case IDictionary map:
                if (current is not IDictionary target)
                {
                    differences.Add($"{path}: was a map");
                    return;
                }

                foreach (DictionaryEntry entry in map)
                {
                    var key = entry.Key.ToString()!;
                    var childPath = path.Length == 0 ? key : $"{path}.{key}";
                    if (!target.Contains(entry.Key))
                    {
                        differences.Add($"{childPath}: removed");
                        continue;
                    }

                    if (AllowedChangedDescriptions.Contains(childPath)) continue;
                    Contained(entry.Value, target[entry.Key], childPath, differences);
                }

                return;
            case IList list:
                if (current is not IList targetList)
                {
                    differences.Add($"{path}: was a list");
                    return;
                }

                var strict = path.EndsWith(".required", StringComparison.Ordinal) || path.EndsWith(".enum", StringComparison.Ordinal);
                if (strict && list.Count != targetList.Count)
                {
                    differences.Add($"{path}: list changed ({list.Count} → {targetList.Count} items)");
                    return;
                }

                for (var i = 0; i < list.Count; i++)
                {
                    var match = targetList.Cast<object?>().Any(candidate => Equal(list[i], candidate));
                    if (!match) differences.Add($"{path}[{i}]: item removed or changed");
                }

                return;
            default:
                if (!Equal(v1, current)) differences.Add($"{path}: '{v1}' → '{current}'");
                return;
        }
    }

    private static bool Equal(object? a, object? b)
    {
        var differences = new List<string>();
        if (a is IDictionary || a is IList)
        {
            Contained(a, b, "", differences);
            return differences.Count == 0;
        }

        return string.Equals(a?.ToString(), b?.ToString(), StringComparison.Ordinal);
    }

    private static Dictionary<object, object> Load(string path)
        => new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(File.ReadAllText(path));

    private static IDictionary Map(object? value) => (IDictionary)value!;

    private static IList List(object? value) => (IList)value!;
}

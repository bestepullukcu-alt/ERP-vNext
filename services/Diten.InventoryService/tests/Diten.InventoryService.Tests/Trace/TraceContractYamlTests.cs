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
/// <para>WP-0174-P4 (CT K4-1): the 1.2.0 step is additive as well — <c>getManualLotMigrationStatus</c>. The 1.1.0 file is
/// pinned in <c>Contract/Fixtures/trace-v1.1.0.openapi.yaml</c> (SHA-256 of the git blob at 366d16ee3, LF-normalised) and
/// every 1.1.0 element must be present and equal in 1.2.0; only <c>info.version</c> may differ.</para>
/// <para>WP-0174-ITEM: 1.3.0 is additive too — <c>SkuLevel</c> gains <c>Item</c> (appended) and <c>createLot</c> gains two error
/// codes. The 1.2.0 file is pinned in <c>Contract/Fixtures/trace-v1.2.0.openapi.yaml</c> (the blob at 9697fda76, LF-normalised).</para>
/// </summary>
public sealed class TraceContractYamlTests
{
    private const string FrozenV1Sha256 = "2fa5e20b8de84deba16533c24bc9e1a090847c9494d73a1752522e1f69ea2297";

    private const string FrozenV11Sha256 = "9ab20cc7bff48205de10e5586a88975641b27b253203c5c57f20427197fb8e79";

    private const string FrozenV12Sha256 = "2bcc0b44656eefa63df7ee142d56df4eabc794a461281029dd48df4a9c32a0b9";

    /// <summary>The 1.2.0 createLot codes; 1.3.0 appends exactly <see cref="AddedCreateLotCodes"/>.</summary>
    private static readonly string[] V12CreateLotCodes =
        ["INVALID_REQUEST", "LOT_NUMBER_INVALID", "LOT_DATES_INVALID", "LOT_SKU_REQUIRED", "LOT_SKU_MISMATCH", "UNKNOWN_ITEM", "PRODUCT_NOT_LOT_CONTROLLED",
         "LOT_ALREADY_EXISTS", "IDEMPOTENCY_KEY_REUSED", "PRODUCT_MASTER_UNAVAILABLE"];

    private static readonly string[] AddedCreateLotCodes = ["PRODUCT_MASTER_DATA_INCOMPLETE", "SKU_LEVEL_NOT_STOCKABLE"];

    /// <summary>The v1 texts B-1 rewrites (accepted proposal §8) — nothing else of v1 may differ.</summary>
    private static readonly HashSet<string> AllowedChangedDescriptions = new(StringComparer.Ordinal)
    {
        "info.version", // 1.0.0 → 1.2.0 (asserted exactly by Version_and_changelog_…)
        "info.description",
        "components.schemas.LotRef.properties.lotNumber.description",
        "components.responses.LotConflict.description"
    };

    /// <summary>
    /// 1.1.0 → 1.2.0: nothing of 1.1.0 may change except its version number; 1.3.0 (WP-0174-ITEM) also appends <c>Item</c> to the
    /// <c>SkuLevel</c> enum — measured exactly by <see cref="The_1_3_0_additions_are_Item_and_the_two_createLot_codes"/>.
    /// </summary>
    private static readonly HashSet<string> AllowedChangedSinceV11 = new(StringComparer.Ordinal) { "info.version", "components.schemas.SkuLevel.enum" };

    private static string FixtureV12Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "trace-v1.2.0.openapi.yaml");

    private static string FixturePath => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "trace-v1.0.0.openapi.yaml");

    private static string FixtureV11Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "trace-v1.1.0.openapi.yaml");

    [Fact]
    public void The_v1_fixture_is_the_frozen_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixturePath).Replace("\r\n", "\n");
        Assert.Equal(FrozenV1Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    [Fact]
    public void The_v1_1_fixture_is_the_1_1_0_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixtureV11Path).Replace("\r\n", "\n");
        Assert.Equal(FrozenV11Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
        Assert.Equal("1.1.0", Map(Load(FixtureV11Path)["info"])["version"]);
    }

    [Fact]
    public void The_v1_2_fixture_is_the_1_2_0_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixtureV12Path).Replace("\r\n", "\n");
        Assert.Equal(FrozenV12Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
        Assert.Equal("1.2.0", Map(Load(FixtureV12Path)["info"])["version"]);
    }

    [Fact]
    public void Every_1_2_0_element_is_present_and_unchanged_in_1_3_0()
    {
        var differences = new List<string>();
        Contained(Load(FixtureV12Path), Load(ContractSchemas.TraceContractPath), "", differences, AllowedChangedSinceV11);
        Assert.True(differences.Count == 0, "1.2.0 elements removed or changed:\n" + string.Join("\n", differences));
    }

    [Fact]
    public void The_1_3_0_additions_are_Item_and_the_two_createLot_codes()
    {
        var v12 = Load(FixtureV12Path);
        var current = Load(ContractSchemas.TraceContractPath);
        Assert.Equal(["Gsku", "Lsku", "FinishedGood"], List(Map(Map(Map(v12["components"])["schemas"])["SkuLevel"])["enum"]).Cast<string>());
        Assert.Equal(["Gsku", "Lsku", "FinishedGood", "Item"], List(Map(Map(Map(current["components"])["schemas"])["SkuLevel"])["enum"]).Cast<string>());

        static IEnumerable<string> CreateLotCodes(Dictionary<object, object> file)
            => List(Map(Map(Map(file["paths"])["/lots"])["post"])["x-error-codes"]).Cast<string>();
        Assert.Equal(V12CreateLotCodes, CreateLotCodes(v12));
        Assert.Equal([.. V12CreateLotCodes, .. AddedCreateLotCodes], CreateLotCodes(current));

        // no other operation's codes changed: every x-error-codes list except createLot's is the 1.2.0 list
        foreach (DictionaryEntry path in Map(current["paths"]))
        foreach (DictionaryEntry op in Map(path.Value))
        {
            if ((string)path.Key == "/lots" && (string)op.Key == "post") continue;
            var now = Map(op.Value).Contains("x-error-codes") ? List(Map(op.Value)["x-error-codes"]).Cast<string>().ToList() : [];
            var before = Map(Map(v12["paths"])[path.Key])[op.Key] is IDictionary b && b.Contains("x-error-codes") ? List(b["x-error-codes"]).Cast<string>().ToList() : [];
            Assert.True(before.SequenceEqual(now), $"{path.Key} {op.Key}: x-error-codes changed");
        }
    }

    [Fact]
    public void Every_1_1_0_element_is_present_and_unchanged_in_1_2_0()
    {
        var v11 = Load(FixtureV11Path);
        var current = Load(ContractSchemas.TraceContractPath);
        var differences = new List<string>();
        Contained(v11, current, "", differences, AllowedChangedSinceV11);
        Assert.True(differences.Count == 0, "1.1.0 elements removed or changed:\n" + string.Join("\n", differences));
    }

    [Fact]
    public void The_1_2_0_status_operation_is_read_with_trace_lots_read()
    {
        var current = Load(ContractSchemas.TraceContractPath);
        var get = Map(Map(Map(current["paths"])["/migration/status"])["get"]);
        Assert.Equal("getManualLotMigrationStatus", get["operationId"]);
        Assert.Equal("trace.lots.read", get["x-permission"]);
        var schema = Map(Map(Map(current["components"])["schemas"])["ManualLotMigrationStatus"]);
        Assert.Equal(["manualLotEntryClosed", "contractVersion"], List(schema["required"]).Cast<string>());
        Assert.Equal("boolean", Map(Map(schema["properties"])["manualLotEntryClosed"])["type"]);
    }

    [Fact]
    public void Version_and_changelog_name_1_3_0_and_the_three_behaviour_changes()
    {
        var current = Load(ContractSchemas.TraceContractPath);
        var info = Map(current["info"]);
        Assert.Equal("1.3.0", info["version"]);
        Assert.Equal("FROZEN", info["x-status"]);
        Assert.Equal("MOD-0174", info["x-owner"]);
        var changelog = string.Join("\n", List(info["x-changelog"]).Cast<string>());
        Assert.Contains("1.0.0", changelog);
        Assert.Contains("1.1.0", changelog);
        Assert.Contains("1.2.0", changelog);
        Assert.Contains("1.3.0", changelog);
        foreach (var marker in new[] { "B-1", "B-2", "B-3", "LOT_SKU_REQUIRED", "K4-1", "getManualLotMigrationStatus", "WP-0174-ITEM", "Item",
                     "PRODUCT_MASTER_DATA_INCOMPLETE", "SKU_LEVEL_NOT_STOCKABLE" }) Assert.Contains(marker, changelog);
    }

    [Fact]
    public void Every_v1_element_is_present_and_unchanged_in_1_1_0()
    {
        var v1 = Load(FixturePath);
        var current = Load(ContractSchemas.TraceContractPath);
        var differences = new List<string>();
        Contained(v1, current, "", differences, AllowedChangedDescriptions);
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
            "listManualLotGroups", "commitManualLotMigration", "getMovementLotLinks", "getManualLotMigrationStatus"
        ];
        Assert.All(expected, op => Assert.Contains(op, operations));
    }

    /// <summary>
    /// Every key / item of <paramref name="v1"/> must exist in <paramref name="current"/> with an equal scalar. Maps may
    /// GAIN keys (additive); lists must keep their v1 items (a parameter list may gain items, a required / enum list may
    /// not change).
    /// </summary>
    private static void Contained(object? v1, object? current, string path, List<string> differences, IReadOnlySet<string> allowed)
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

                    if (allowed.Contains(childPath)) continue;
                    Contained(entry.Value, target[entry.Key], childPath, differences, allowed);
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
                    var match = targetList.Cast<object?>().Any(candidate => Equal(list[i], candidate, allowed));
                    if (!match) differences.Add($"{path}[{i}]: item removed or changed");
                }

                return;
            default:
                if (!Equal(v1, current, allowed)) differences.Add($"{path}: '{v1}' → '{current}'");
                return;
        }
    }

    private static bool Equal(object? a, object? b, IReadOnlySet<string> allowed)
    {
        var differences = new List<string>();
        if (a is IDictionary || a is IList)
        {
            Contained(a, b, "", differences, allowed);
            return differences.Count == 0;
        }

        return string.Equals(a?.ToString(), b?.ToString(), StringComparison.Ordinal);
    }

    private static Dictionary<object, object> Load(string path)
        => new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(File.ReadAllText(path));

    private static IDictionary Map(object? value) => (IDictionary)value!;

    private static IList List(object? value) => (IList)value!;
}

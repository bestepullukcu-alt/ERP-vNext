using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Contract;

/// <summary>
/// WP-0174-P2 — the INVENTORY-BUNDLE single-writer step 1.0.0 → 1.0.1 is DESCRIPTION-ONLY (MOD-0174 CT-D1 (a), owner
/// decision 2026-10-05). The frozen 1.0.0 file is pinned in <c>Contract/Fixtures/inventory-bundle-v1.0.0.openapi.yaml</c>
/// (the git blob at 5f2e5b4f9, SHA-256 LF-normalised). Both files are flattened to path → scalar and compared BOTH ways:
/// the only differences allowed are <c>info.version</c> (1.0.0 → 1.0.1), the new <c>info.x-changelog</c> and the new
/// <c>AvailabilityView.available</c> description. Any path, schema, property, type, enum, required list or response change
/// fails here.
/// <para>WP-0175-Q4 — the step 1.0.1 → 1.0.2 is description-only too (MOD-0175 CT-Q3 (a), owner decision 2026-10-07, same form as
/// CT-D1): the version, one changelog line, the retest-due words appended to the same <c>available</c> description, and one more
/// example (<c>lotRetestDue</c>) under the existing <c>UnprocessableMovement</c> examples.</para>
/// </summary>
public sealed class InventoryBundleDescriptionNoteTests
{
    private const string FrozenV100Sha256 = "456f63589ee1141dbe1ad999eda090b22c22af3ae1de07e1fcd0616ce44db4d8";
    private const string AvailableDescription = "components.schemas.AvailabilityView.properties.available.description";
    private const string RetestDueExample = "components.responses.UnprocessableMovement.content.application/json.examples.lotRetestDue.";

    private static string FixturePath => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "inventory-bundle-v1.0.0.openapi.yaml");

    [Fact]
    public void The_fixture_is_the_frozen_1_0_0_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixturePath).Replace("\r\n", "\n");
        Assert.Equal(FrozenV100Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    [Fact]
    public void Only_the_version_the_changelog_and_the_available_description_differ_from_1_0_0()
    {
        var frozen = Flatten(Load(FixturePath));
        var current = Flatten(Load(ContractSchemas.ContractPath));
        Assert.True(frozen.Count > 300, "the flattener found too little to compare: " + frozen.Count);

        var changed = frozen.Keys.Where(k => !current.TryGetValue(k, out var v) || !string.Equals(v, frozen[k], StringComparison.Ordinal)).ToList();
        var added = current.Keys.Where(k => !frozen.ContainsKey(k)).ToList();

        Assert.Equal(["info.version"], changed);
        Assert.Equal("1.0.2", current["info.version"]);
        Assert.All(added, k => Assert.True(k == AvailableDescription || k.StartsWith("info.x-changelog[", StringComparison.Ordinal)
                                           || k.StartsWith(RetestDueExample, StringComparison.Ordinal), "unexpected addition: " + k));
        Assert.Equal("LOT_RETEST_DUE", current[RetestDueExample + "value.error.code"]);
        Assert.Contains(AvailableDescription, added);
        Assert.Equal("FROZEN", current["info.x-status"]);
    }

    [Fact]
    public void The_note_and_the_changelog_say_what_CT_D1_decided()
    {
        var current = Flatten(Load(ContractSchemas.ContractPath));
        Assert.Contains("excluding Blocked / Expired lots (MOD-0174 CT-D1, 2026-10-06)", current[AvailableDescription]);
        Assert.StartsWith("available = onHand(AVAILABLE) − reserved", current[AvailableDescription]);
        var changelog = string.Join("\n", current.Where(kv => kv.Key.StartsWith("info.x-changelog[", StringComparison.Ordinal)).Select(kv => kv.Value));
        Assert.Contains("1.0.0", changelog);
        Assert.Contains("1.0.1", changelog);
        Assert.Contains("description-only", changelog);
        // WP-0175-Q4 (CT-Q3): the same available description also names the retest-due lots; the changelog names the code.
        Assert.EndsWith("and retest-due lots (MOD-0175 CT-Q3, 2026-10-08)", current[AvailableDescription]);
        Assert.Contains("1.0.2", changelog);
        Assert.Contains("LOT_RETEST_DUE", changelog);
        // The row-level BalanceView.available keeps its 1.0.0 text: a Blocked lot's row shows its own available unchanged.
        Assert.Equal("available = onHand(AVAILABLE) − reserved", current["components.schemas.BalanceView.properties.available.description"]);
    }

    private static Dictionary<string, string> Flatten(object? node)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        Walk(node, "", result);
        return result;
    }

    private static void Walk(object? node, string path, Dictionary<string, string> into)
    {
        switch (node)
        {
            case IDictionary map:
                foreach (DictionaryEntry entry in map)
                {
                    Walk(entry.Value, path.Length == 0 ? entry.Key.ToString()! : $"{path}.{entry.Key}", into);
                }

                break;
            case IList list:
                for (var i = 0; i < list.Count; i++) Walk(list[i], $"{path}[{i}]", into);
                break;
            default:
                into[path] = node?.ToString() ?? "~";
                break;
        }
    }

    private static Dictionary<object, object> Load(string path)
        => new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(File.ReadAllText(path));
}

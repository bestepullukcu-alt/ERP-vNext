using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S4 — the contract file the schema tests read. v1.1 is not on this branch: the YAML is copied byte for
/// byte from the contract line (1.1.0-draft.7, kanban-preview bcae90317 — CT's OD-1 / X-08 changes of 2026-10-09) and
/// pinned by SHA-256; the JSON next to it is
/// its rendering (YAML → JSON, no edit) and is tied back to the YAML by every name it carries. When CT changes the
/// contract, these pins go red first and the fixture is copied again.
/// </summary>
public sealed class ProductMasterContractFixtureTests
{
    public const string V11YamlSha256 = "bb6b7ac0ae1cf456a0e61203507dcee197559ef01c746fcff6acf5b4070a21cf";
    public const string V11JsonSha256 = "a0143747889d0570ebad0956d83ad4bddc07329c19e579f25d83bf8c2deb5250";
    public const string V10JsonSha256 = "9dec887b5b124acfa8d022df155789113fded5abc06e976b41366a1950182e8d";
    // The frozen v1.0 (the contract line's product-master-bundle.v1.0.openapi.yaml; measured byte-identical to the v1.0
    // file this branch carries). Copied too, so the test never reads outside its fixtures.
    public const string V10YamlSha256 = "8c5ed4679319be39f73ad6b03fa89beb734ea16d8607cdb1c5b53844a383ca3a";

    [Fact]
    public void S4_Fixture_v11_yaml_is_the_contract_draft7_byte_for_byte() =>
        Assert.Equal(V11YamlSha256, Sha256(ProductMasterContractSchema.FixturePath(ProductMasterContractSchema.V11Yaml)));

    [Fact]
    public void S4_Fixture_json_renderings_are_pinned()
    {
        Assert.Equal(V11JsonSha256, Sha256(ProductMasterContractSchema.FixturePath(ProductMasterContractSchema.V11Json)));
        Assert.Equal(V10JsonSha256, Sha256(ProductMasterContractSchema.FixturePath(ProductMasterContractSchema.V10Json)));
    }

    [Fact]
    public void S4_Fixture_v10_yaml_is_the_frozen_v10() =>
        Assert.Equal(V10YamlSha256, Sha256(ProductMasterContractSchema.FixturePath(ProductMasterContractSchema.V10Yaml)));

    // The JSON is the YAML's rendering: every path, operation, schema, property and enum value it names is in the YAML.
    [Fact]
    public void S4_Fixture_json_names_only_what_the_yaml_says()
    {
        var yaml = File.ReadAllText(ProductMasterContractSchema.FixturePath(ProductMasterContractSchema.V11Yaml));
        var root = ProductMasterContractSchema.V11.Root;
        Assert.Equal("1.1.0-draft.7", root.GetProperty("info").GetProperty("version").GetString());
        var names = new List<string>();
        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            names.Add(path.Name);
            foreach (var operation in path.Value.EnumerateObject())
                if (operation.Value.TryGetProperty("operationId", out var id)) names.Add(id.GetString()!);
        }
        foreach (var schema in root.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            names.Add(schema.Name + ":");
            if (schema.Value.TryGetProperty("properties", out var properties))
                names.AddRange(properties.EnumerateObject().Select(x => x.Name + ":"));
            if (schema.Value.TryGetProperty("enum", out var values))
                names.AddRange(values.EnumerateArray().Select(x => x.GetString()!));
        }
        Assert.True(names.Count > 100, $"only {names.Count} names read");
        Assert.All(names, name => Assert.Contains(name, yaml, StringComparison.Ordinal));
    }

    // The validator itself: every example the contract carries passes its own schema (the contract says it was checked);
    // a broken answer does not.
    [Fact]
    public void S4_Validator_accepts_every_contract_example()
    {
        var schema = ProductMasterContractSchema.V11;
        var checkedExamples = 0;
        foreach (var path in schema.Root.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
        {
            if (!operation.Value.TryGetProperty("operationId", out var id)) continue;
            foreach (var response in operation.Value.GetProperty("responses").EnumerateObject())
            {
                if (!response.Value.TryGetProperty("content", out var content)
                    || !content.GetProperty("application/json").TryGetProperty("examples", out var examples)) continue;
                foreach (var example in examples.EnumerateObject())
                {
                    var failures = schema.ValidateResponse(id.GetString()!, int.Parse(response.Name),
                        example.Value.GetProperty("value"));
                    Assert.True(failures.Count == 0, $"{id.GetString()} / {example.Name}: {string.Join("; ", failures)}");
                    checkedExamples++;
                }
            }
        }
        Assert.True(checkedExamples >= 15, $"only {checkedExamples} examples");
    }

    [Theory]
    [InlineData("""{"skuId":"not-a-uuid","skuLevel":"Item","contractVersion":"v1.1"}""", "is not a uuid")]
    [InlineData("""{"skuId":"d4e5f6a7-0000-0000-0000-000000000003","skuLevel":"Box","contractVersion":"v1.1"}""", "is not in the enum")]
    [InlineData("""{"skuId":"d4e5f6a7-0000-0000-0000-000000000003","lotControlled":"yes","contractVersion":"v1.1"}""", "is not boolean|null")]
    [InlineData("""{"skuId":"d4e5f6a7-0000-0000-0000-000000000003","shelfLifeDaysSource":null}""", "null is not allowed")]
    [InlineData("""{"skuId":"d4e5f6a7-0000-0000-0000-000000000003","onHand":5}""", "'onHand' is not in the contract")]
    [InlineData("""{"skuId":"d4e5f6a7-0000-0000-0000-000000000003","packQuantity":"1e3"}""", "does not match")]
    public void S4_Validator_refuses_a_wrong_sku_answer(string body, string expected)
    {
        using var document = JsonDocument.Parse(body);
        var failures = ProductMasterContractSchema.V11.ValidateResponse("getSku", 200, document.RootElement);
        Assert.Contains(failures, failure => failure.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void S4_Validator_refuses_a_stockable_row_without_its_required_fields()
    {
        using var document = JsonDocument.Parse(
            """{"items":[{"skuId":"d4e5f6a7-0000-0000-0000-000000000003","skuLevel":"Item"}],"page":1,"pageSize":25,"totalCount":1,"contractVersion":"v1.1"}""");
        var failures = ProductMasterContractSchema.V11.ValidateResponse("searchStockableItems", 200, document.RootElement);
        Assert.Contains(failures, failure => failure.Contains("required 'itemKind' is missing", StringComparison.Ordinal));
    }

    internal static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;
using static Diten.ProcurementService.Api.Tests.Suppliers.Contract.SupplierContractChecks;

namespace Diten.ProcurementService.Api.Tests.Suppliers.Contract;

/// <summary>
/// MOD-0140 DL-3 (§2 "1.4.0", §16 Revizyon 3 S-6, §17 "Contract (Revizyon 3, DL-3)"): SUPPLIER 1.4.0 is additive only against
/// the frozen 1.3.0 file — every 1.3.0 node is unchanged except the closed list below — and adds exactly the K-1 relationship
/// surface: four operations, three schemas, one response. No service-token scheme (1.5.0, DL-7), no tenant-wide tax-number
/// search (DL-3's deferred part). Measured on the real file and on sabotages.
/// </summary>
public sealed class SupplierContract140Tests
{
    /// <summary>SHA-256 of 1.3.0 as frozen on main by DL-1 (the fixture is that file).</summary>
    private const string Frozen130Sha256 = "7204e6d4d795218ce1560d6e6a41ee51b709c69830831606c59fc7730bfd7035";

    private const string UpdateSupplierDescription = "/paths//{supplierId}/patch/description";

    private static readonly string ContractText = ReadContract();
    private static readonly string Frozen130Text = Read(Frozen130RelativePath);

    private static readonly string[] AddedPaths = ["/{supplierId}/relationship", "/{supplierId}/relationship/status", "/{supplierId}/relationship/payment-terms"];
    private static readonly string[] AddedSchemas = ["SupplierRelationship", "RelationshipStatusChange", "RelationshipPaymentTerms"];
    private static readonly string[] AddedResponses = ["RelationshipExists"];

    [Fact]
    public void The_fixture_is_the_frozen_1_3_0_file()
    {
        Assert.Equal(Frozen130Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Frozen130Text))).ToLowerInvariant());
        Assert.Equal("1.3.0", (string?)Parse(Frozen130Text)["info"]!["version"]);
    }

    [Fact]
    public void The_contract_is_1_4_0_frozen() =>
        Assert.Equal(("1.4.0", "FROZEN", "FROZEN"), (
            (string?)Parse(ContractText)["info"]!["version"],
            (string?)Parse(ContractText)["info"]!["x-status"],
            (string?)Parse(ContractText)["info"]!["x-slice-status"]));

    [Fact]
    public void Against_1_3_0_nothing_changes_but_the_closed_list_and_only_the_K1_surface_is_added() =>
        Assert.Empty(AdditiveViolations(Parse(Frozen130Text), Parse(ContractText)));

    [Fact]
    public void The_new_operations_are_the_four_K1_names() =>
        Assert.Equal(
            ["changeRelationshipStatus", "getSupplierRelationship", "linkSupplierToCompany", "setRelationshipPaymentTerms"],
            Operations(Parse(ContractText)).Select(o => (string)o.Operation["operationId"]!)
                .Except(Operations(Parse(Frozen130Text)).Select(o => (string)o.Operation["operationId"]!))
                .Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void No_service_token_scheme_and_no_tenant_wide_search()
    {
        var doc = Parse(ContractText);
        Assert.Equal(["bearerAuth"], doc["components"]!["securitySchemes"]!.AsObject().Select(p => p.Key).ToArray());
        Assert.DoesNotContain(doc["paths"]!.AsObject(), p => p.Key.Contains("tax", StringComparison.OrdinalIgnoreCase) || p.Key.Contains("tenant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Examples_still_violate_only_the_two_known_1_2_0_places() =>
        Assert.Equal(KnownExampleViolations, ExampleViolations(Parse(ContractText)));

    [Fact]
    public void The_new_schemas_spell_null_the_2020_12_way()
    {
        var schemas = Parse(ContractText)["components"]!["schemas"]!;
        Assert.All(AddedSchemas, name => Assert.DoesNotContain("nullable", schemas[name]!.ToJsonString(), StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------- sabotage (pack §17)
    [Fact]
    public void Sabotage_a_removed_1_3_0_property_turns_the_diff_red() =>
        Assert.NotEmpty(AdditiveViolations(Parse(Frozen130Text), Parse(Sabotage(
            "        relationshipExists: { type: boolean, description: \"çağıran şirketle silinmemiş ilişki var mı\" }\n", string.Empty))));

    [Fact]
    public void Sabotage_a_changed_1_3_0_response_turns_the_diff_red() =>
        Assert.NotEmpty(AdditiveViolations(Parse(Frozen130Text), Parse(Sabotage(
            "      operationId: getSupplierEligibility\n", "      operationId: getSupplierEligibilityV2\n"))));

    [Fact]
    public void Sabotage_an_extra_path_turns_the_diff_red() =>
        Assert.NotEmpty(AdditiveViolations(Parse(Frozen130Text), Parse(Sabotage(
            "  /{supplierId}/relationship/status:\n",
            "  /by-tax-id:\n    get:\n      operationId: findTenantSupplierByTaxId\n      responses: { '200': { description: x } }\n  /{supplierId}/relationship/status:\n"))));

    [Fact]
    public void Sabotage_a_rewritten_update_supplier_description_turns_the_diff_red() =>
        Assert.NotEmpty(AdditiveViolations(Parse(Frozen130Text), Parse(Sabotage(
            "403 gövdesinde error.code MASTER_UPDATE_FORBIDDEN olabilir", "403 gövdesinde error.code FORBIDDEN olabilir"))));

    /// <summary>
    /// Every 1.3.0 node equal in 1.4.0, except: <c>info.version</c>, <c>info.x-producer-status</c>, <c>info.description</c>
    /// (1.3.0 text as prefix, a paragraph appended) and <c>updateSupplier.description</c> (1.3.0 text as prefix, the
    /// MASTER_UPDATE_FORBIDDEN sentence appended — F-4). Added nodes: only the three paths, three schemas and one response.
    /// </summary>
    public static List<string> AdditiveViolations(JsonObject v130, JsonObject v140)
    {
        var violations = new List<string>();
        Compare(v130, v140, string.Empty, violations);

        Added(v130["paths"]!.AsObject(), v140["paths"]!.AsObject(), AddedPaths, "/paths", violations);
        Added(v130["components"]!["schemas"]!.AsObject(), v140["components"]!["schemas"]!.AsObject(), AddedSchemas, "/components/schemas", violations);
        Added(v130["components"]!["responses"]!.AsObject(), v140["components"]!["responses"]!.AsObject(), AddedResponses, "/components/responses", violations);
        foreach (var (key, _) in v140.Where(p => !v130.ContainsKey(p.Key)))
        {
            violations.Add($"/{key}: new top-level key");
        }

        foreach (var key in new[] { "securitySchemes", "parameters" })
        {
            Added(v130["components"]![key]!.AsObject(), v140["components"]![key]!.AsObject(), [], "/components/" + key, violations);
        }

        return violations;
    }

    private static void Compare(JsonNode? old, JsonNode? now, string at, List<string> violations)
    {
        switch (at)
        {
            case "/info/version" or "/info/x-producer-status":
                return;
            case "/info/description":
                Prefix(old, now, at, null, violations);
                return;
            case UpdateSupplierDescription:
                Prefix(old, now, at, "MASTER_UPDATE_FORBIDDEN", violations);
                return;
        }

        if (old is JsonObject oldObj)
        {
            if (now is not JsonObject newObj)
            {
                violations.Add($"{at}: mapping replaced");
                return;
            }

            foreach (var (key, value) in oldObj)
            {
                if (!newObj.ContainsKey(key))
                {
                    violations.Add($"{at}/{key}: removed");
                    continue;
                }

                Compare(value, newObj[key], $"{at}/{key}", violations);
            }

            return;
        }

        if (!JsonNode.DeepEquals(old, now))
        {
            violations.Add($"{at}: changed");
        }
    }

    private static void Prefix(JsonNode? old, JsonNode? now, string at, string? mustAdd, List<string> violations)
    {
        var before = (string?)old ?? string.Empty;
        var after = (string?)now ?? string.Empty;
        if (!after.StartsWith(before, StringComparison.Ordinal) || after.Length == before.Length
            || (mustAdd is not null && !after[before.Length..].Contains(mustAdd, StringComparison.Ordinal)))
        {
            violations.Add($"{at}: the 1.3.0 text is not kept as prefix with the allowed addition");
        }
    }

    private static void Added(JsonObject old, JsonObject now, string[] allowed, string at, List<string> violations)
    {
        var added = now.Select(p => p.Key).Where(k => !old.ContainsKey(k)).Order(StringComparer.Ordinal).ToArray();
        if (!added.SequenceEqual(allowed.Order(StringComparer.Ordinal)))
        {
            violations.Add($"{at}: added [{string.Join(", ", added)}], allowed [{string.Join(", ", allowed)}]");
        }
    }

    private static string Sabotage(string from, string to)
    {
        Assert.Equal(1, ContractText.Split(from).Length - 1);
        return ContractText.Replace(from, to, StringComparison.Ordinal);
    }
}

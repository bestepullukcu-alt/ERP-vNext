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
/// <para>WP-0173-PM2 — the step 1.0.2 → 1.1.0 is ADDITIVE (MOD-0290 R-12, proposal accepted 2026-10-09): one more <c>SkuLevel</c>
/// value (<c>Item</c>, appended — the three 1.0.0 values keep their positions) with the proposal's description, the new path
/// <c>/stock-presence</c> (<c>getStockPresence</c>), the <c>internalApiKey</c> security scheme and the <c>StockPresence</c> schema.
/// Still: no 1.0.0 key removed and none changed except the version and the <c>SkuLevel</c> description.</para>
/// <para>WP-0173-PM1 — the step 1.1.0 → 1.2.0 is ADDITIVE too (CT-PM1-1): three examples under the existing
/// <c>UnprocessableMovement</c> response (codes PRODUCT_MASTER_DATA_INCOMPLETE, ITEM_NOT_ACTIVE, SKU_LEVEL_NOT_STOCKABLE), a
/// <c>description</c> on <c>postMovement</c> and one changelog line — nothing else.</para>
/// <para>WP-0174-ITEM — 1.2.0 → 1.2.1 is DESCRIPTION ONLY (O-PM1-3): the postMovement description names the stockable: false refusal
/// and one changelog line is added; measured against the pinned 1.2.0 file (<c>Fixtures/inventory-bundle-v1.2.0.openapi.yaml</c>).</para>
/// <para>WP-0177-R3 — 1.3.0 → 1.4.0 is ADDITIVE (MOD-0177 KR-C / CT-R10, owner step MOD-0173 + MOD-0174): the 422 example
/// <c>lotUnderRecall</c>, the recall / CT-R10 words appended to the <c>postMovement</c> description and one changelog line; measured against
/// the pinned 1.3.0 file (<c>Fixtures/inventory-bundle-v1.3.0.openapi.yaml</c>, the git blob at f6b6ee435).</para>
/// </summary>
public sealed class InventoryBundleDescriptionNoteTests
{
    private const string FrozenV100Sha256 = "456f63589ee1141dbe1ad999eda090b22c22af3ae1de07e1fcd0616ce44db4d8";
    private const string AvailableDescription = "components.schemas.AvailabilityView.properties.available.description";
    private const string RetestDueExample = "components.responses.UnprocessableMovement.content.application/json.examples.lotRetestDue.";
    private const string SkuLevelDescription = "components.schemas.SkuLevel.description";


    /// <summary>The 1.3.0 additions (WP-0176-F2, MOD-0176 KF-B / KF-C, CT-F1): exactly these subtrees, nothing else.</summary>
    private static readonly string[] V130Additions =
    [
        "components.schemas.MovementRequest.properties.fefoBypassReason.", "components.schemas.MovementResponse.properties.expiryChecks.",
        "components.schemas.ExpiryChecks.", UnprocessableExamples + "fefoBypassReasonRequired.", UnprocessableExamples + "fefoOrderViolation.",
        UnprocessableExamples + "lotBelowMinRemainingShelfLife.", UnprocessableExamples + "lotExpired.", UnprocessableExamples + "lotBlocked."
    ];

    /// <summary>The 1.1.0 additions (WP-0173-PM2): exactly these subtrees, nothing else.</summary>
    private static readonly string[] AdditivePrefixes =
    [
        "paths./stock-presence.", "components.schemas.StockPresence.", "components.securitySchemes.internalApiKey.",
        // 1.2.0 (WP-0173-PM1)
        UnprocessableExamples + "productMasterDataIncomplete.", UnprocessableExamples + "itemNotActive.", UnprocessableExamples + "skuLevelNotStockable.",
        // 1.3.0 (WP-0176-F2)
        .. V130Additions,
        // 1.4.0 (WP-0177-R3)
        UnprocessableExamples + "lotUnderRecall."
    ];

    private const string UnprocessableExamples = "components.responses.UnprocessableMovement.content.application/json.examples.";
    private const string PostMovementDescription = "paths./movements.post.description";

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

        Assert.Equal(["info.version", SkuLevelDescription], changed);
        Assert.Equal("1.4.0", current["info.version"]);
        Assert.All(added, k => Assert.True(k == AvailableDescription || k.StartsWith("info.x-changelog[", StringComparison.Ordinal)
                                           || k.StartsWith(RetestDueExample, StringComparison.Ordinal)
                                           || k == "components.schemas.SkuLevel.enum[3]" || k == PostMovementDescription
                                           || AdditivePrefixes.Any(p => k.StartsWith(p, StringComparison.Ordinal)), "unexpected addition: " + k));
        Assert.Equal("LOT_RETEST_DUE", current[RetestDueExample + "value.error.code"]);
        Assert.Contains(AvailableDescription, added);
        Assert.Equal("FROZEN", current["info.x-status"]);
    }

    private const string FrozenV120Sha256 = "e2d87c0bb2f303be905b5a0e34e71537d8ba053e3ce2fde7acb0d30f0eb8965e";

    private static string FixtureV120Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "inventory-bundle-v1.2.0.openapi.yaml");

    [Fact]
    public void The_1_2_0_fixture_is_the_frozen_1_2_0_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixtureV120Path).Replace("\r\n", "\n");
        Assert.Equal(FrozenV120Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    [Fact]
    public void The_1_2_1_change_is_the_postMovement_description_and_one_changelog_line()
    {
        var frozen = Flatten(Load(FixtureV120Path));
        var current = Flatten(Load(FixtureV121Path));
        var changed = frozen.Keys.Where(k => !current.TryGetValue(k, out var v) || !string.Equals(v, frozen[k], StringComparison.Ordinal)).ToList();
        var added = current.Keys.Where(k => !frozen.ContainsKey(k)).ToList();
        Assert.Equal(["info.version", PostMovementDescription], changed);
        Assert.Equal("1.2.0", frozen["info.version"]);
        Assert.Equal("1.2.1", current["info.version"]);
        var line = Assert.Single(added);
        Assert.StartsWith("info.x-changelog[", line);
        Assert.StartsWith("1.2.1 — description only (WP-0174-ITEM, O-PM1-3)", current[line]);
        Assert.Contains("stockable: false", current[PostMovementDescription]);
        Assert.Contains("SKU_LEVEL_NOT_STOCKABLE", current[PostMovementDescription]);
    }

    /// <summary>WP-0176-F2: the 1.2.1 file as frozen by WP-0174-ITEM, kept byte for byte (LF-normalised).</summary>
    private const string FrozenV121Sha256 = "ac2781a8445d3b0f724ec7d4032c5ce5f75c70b88cf48115f1dd016f84052369";

    private static string FixtureV121Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "inventory-bundle-v1.2.1.openapi.yaml");

    [Fact]
    public void The_1_2_1_fixture_is_the_frozen_1_2_1_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixtureV121Path).Replace("\r\n", "\n");
        Assert.Equal(FrozenV121Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    /// <summary>
    /// WP-0176-F2 — 1.2.1 → 1.3.0 is ADDITIVE (MOD-0176 KF-B / KF-C, CT-F1): nothing of 1.2.1 changes but the version; the additions are exactly
    /// the optional request member <c>fefoBypassReason</c>, the optional response member <c>expiryChecks</c> + its schema, five examples under the
    /// existing <c>UnprocessableMovement</c> response (three new 422 codes, LOT_EXPIRED / LOT_BLOCKED described) and one changelog line.
    /// </summary>
    [Fact]
    public void The_1_3_0_change_is_additive_fefo_reason_expiry_checks_and_three_422_codes()
    {
        var frozen = Flatten(Load(FixtureV121Path));
        var current = Flatten(Load(FixtureV130Path)); // WP-0177-R3: the 1.3.0 file as frozen (the live file is 1.4.0)
        var changed = frozen.Keys.Where(k => !current.TryGetValue(k, out var v) || !string.Equals(v, frozen[k], StringComparison.Ordinal)).ToList();
        var added = current.Keys.Where(k => !frozen.ContainsKey(k)).ToList();
        Assert.Equal(["info.version"], changed);
        Assert.Equal("1.3.0", current["info.version"]);
        var lines = added.Where(k => k.StartsWith("info.x-changelog[", StringComparison.Ordinal)).ToList();
        Assert.StartsWith("1.3.0 — additive (WP-0176-F2", current[Assert.Single(lines)]);
        Assert.All(added.Except(lines), k => Assert.True(V130Additions.Any(p => k.StartsWith(p, StringComparison.Ordinal)), "unexpected addition: " + k));
        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", current[UnprocessableExamples + "fefoBypassReasonRequired.value.error.code"]);
        Assert.Equal("FEFO_ORDER_VIOLATION", current[UnprocessableExamples + "fefoOrderViolation.value.error.code"]);
        Assert.Equal("LOT_BELOW_MIN_REMAINING_SHELF_LIFE", current[UnprocessableExamples + "lotBelowMinRemainingShelfLife.value.error.code"]);
        Assert.Equal("QUARANTINE", current[UnprocessableExamples + "lotBelowMinRemainingShelfLife.value.error.details.requiredToStockStatus"]);
        Assert.False(current.ContainsKey("components.schemas.MovementRequest.required[11]"), "fefoBypassReason must stay optional");
        Assert.False(current.Keys.Any(k => k.Contains("appliedToStockStatus", StringComparison.Ordinal)), "CT-F1: the ledger never changes the requested status");
        Assert.All(new[] { "FEFO_BYPASS_REASON_REQUIRED", "FEFO_ORDER_VIOLATION", "LOT_BELOW_MIN_REMAINING_SHELF_LIFE" },
            c => Assert.Contains(c, Diten.InventoryService.Application.Common.InventoryErrorCodes.All));
    }

    /// <summary>WP-0177-R3: the 1.3.0 file as frozen by WP-0176-F2, kept byte for byte (LF-normalised; git blob at f6b6ee435).</summary>
    private const string FrozenV130Sha256 = "c52168402ef12d81a7f07135d8219c0e0634df467ef1f3ec28a3e6af71e04ead";

    private static string FixtureV130Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "inventory-bundle-v1.3.0.openapi.yaml");

    [Fact]
    public void The_1_3_0_fixture_is_the_frozen_1_3_0_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixtureV130Path).Replace("\r\n", "\n");
        Assert.Equal(FrozenV130Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    /// <summary>
    /// WP-0177-R3 — 1.3.0 → 1.4.0 is ADDITIVE (MOD-0177 KR-C / CT-R4 / CT-R7 / CT-R10 / CT-R11): nothing of 1.3.0 changes but the version and the
    /// postMovement description (the 1.3.0 text kept as its prefix, the recall / CT-R10 note appended); the additions are exactly the example
    /// <c>lotUnderRecall</c> under the existing <c>UnprocessableMovement</c> response and one changelog line. The code is in the closed set.
    /// </summary>
    [Fact]
    public void The_1_4_0_change_is_additive_lot_under_recall_and_the_CT_R10_note()
    {
        var frozen = Flatten(Load(FixtureV130Path));
        var current = Flatten(Load(ContractSchemas.ContractPath));
        var changed = frozen.Keys.Where(k => !current.TryGetValue(k, out var v) || !string.Equals(v, frozen[k], StringComparison.Ordinal)).ToList();
        var added = current.Keys.Where(k => !frozen.ContainsKey(k)).ToList();
        Assert.Equal(["info.version", PostMovementDescription], changed);
        Assert.Equal("1.3.0", frozen["info.version"]);
        Assert.Equal("1.4.0", current["info.version"]);
        Assert.StartsWith(frozen[PostMovementDescription], current[PostMovementDescription], StringComparison.Ordinal);
        Assert.All(new[] { "LOT_UNDER_RECALL", "WAREHOUSE_TRANSFER", "never by sourceModule", "CT-R10", "QUARANTINE, QUALITY_INSPECTION or BLOCKED" },
            t => Assert.Contains(t, current[PostMovementDescription], StringComparison.Ordinal));
        var lines = added.Where(k => k.StartsWith("info.x-changelog[", StringComparison.Ordinal)).ToList();
        Assert.StartsWith("1.4.0 — additive (WP-0177-R3", current[Assert.Single(lines)]);
        Assert.All(added.Except(lines), k => Assert.StartsWith(UnprocessableExamples + "lotUnderRecall.", k, StringComparison.Ordinal));
        Assert.Equal("LOT_UNDER_RECALL", current[UnprocessableExamples + "lotUnderRecall.value.error.code"]);
        Assert.All(new[] { "lotId", "lotNumber", "recallCaseId", "recallCaseNumber", "movementType" },
            d => Assert.True(current.ContainsKey(UnprocessableExamples + "lotUnderRecall.value.error.details." + d), "details." + d));
        Assert.Contains("LOT_UNDER_RECALL", Diten.InventoryService.Application.Common.InventoryErrorCodes.All);
    }

    [Fact]
    public void The_1_2_0_additions_are_the_three_422_codes_and_the_postMovement_note()
    {
        var current = Flatten(Load(ContractSchemas.ContractPath));
        Assert.Equal("PRODUCT_MASTER_DATA_INCOMPLETE", current[UnprocessableExamples + "productMasterDataIncomplete.value.error.code"]);
        Assert.Equal("ITEM_NOT_ACTIVE", current[UnprocessableExamples + "itemNotActive.value.error.code"]);
        Assert.Equal("SKU_LEVEL_NOT_STOCKABLE", current[UnprocessableExamples + "skuLevelNotStockable.value.error.code"]);
        Assert.Equal("#/components/responses/UnprocessableMovement", current["paths./movements.post.responses.422.$ref"]);
        Assert.StartsWith("1.2.0 — PRODUCT-MASTER 1.1 rules", current[PostMovementDescription]);
        var changelog = string.Join("\n", current.Where(kv => kv.Key.StartsWith("info.x-changelog[", StringComparison.Ordinal)).Select(kv => kv.Value));
        Assert.Contains("1.2.0 — additive (WP-0173-PM1, CT-PM1-1", changelog);
        // The code list of the service names exactly these three new codes.
        Assert.Contains("PRODUCT_MASTER_DATA_INCOMPLETE", Diten.InventoryService.Application.Common.InventoryErrorCodes.All);
        Assert.Contains("ITEM_NOT_ACTIVE", Diten.InventoryService.Application.Common.InventoryErrorCodes.All);
        Assert.Contains("SKU_LEVEL_NOT_STOCKABLE", Diten.InventoryService.Application.Common.InventoryErrorCodes.All);
    }

    [Fact]
    public void The_1_1_0_additions_are_exactly_the_accepted_proposal()
    {
        var current = Flatten(Load(ContractSchemas.ContractPath));

        // Change 1: Item appended after the three 1.0.0 levels (their positions unchanged), with the proposal's description.
        Assert.Equal(["Gsku", "Lsku", "FinishedGood", "Item"],
            Enumerable.Range(0, 4).Select(i => current[$"components.schemas.SkuLevel.enum[{i}]"]));
        Assert.False(current.ContainsKey("components.schemas.SkuLevel.enum[4]"));
        Assert.StartsWith("Polymorphic stock level. Item = a material item", current[SkuLevelDescription]);

        // Change 2: getStockPresence on its own server (InventoryService directly), service key only, the four statuses.
        const string op = "paths./stock-presence.get.";
        Assert.Equal("/api/internal/inventory", current["paths./stock-presence.servers[0].url"]);
        Assert.Equal("getStockPresence", current[op + "operationId"]);
        // An empty scope list flattens to nothing, so the operation's security is read from the parsed document itself.
        var get = (IDictionary)((IDictionary)((IDictionary)Load(ContractSchemas.ContractPath)["paths"])["/stock-presence"]!)["get"]!;
        var requirement = (IDictionary)Assert.Single((IList)get["security"]!)!;
        Assert.Equal(["internalApiKey"], requirement.Keys.Cast<object>().Select(k => k.ToString()));
        Assert.Equal(["200", "400", "401", "503"],
            current.Keys.Where(k => k.StartsWith(op + "responses.", StringComparison.Ordinal))
                .Select(k => k[(op + "responses.").Length..].Split('.')[0]).Distinct().OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal(["X-Tenant-Id", "skuId", "skuLevel"], Enumerable.Range(0, 3).Select(i => current[$"{op}parameters[{i}].name"]));
        Assert.All(Enumerable.Range(0, 3), i => Assert.Equal("true", current[$"{op}parameters[{i}].required"]));
        Assert.Equal("#/components/schemas/StockPresence", current[op + "responses.200.content.application/json.schema.$ref"]);
        Assert.Equal("INVENTORY_UNAVAILABLE", current[op + "responses.503.content.application/json.example.error.code"]);
        Assert.Equal("X-Internal-Api-Key", current["components.securitySchemes.internalApiKey.name"]);
        Assert.Equal("header", current["components.securitySchemes.internalApiKey.in"]);

        // StockPresence: the six required members and NO quantity member.
        Assert.Equal(["skuId", "skuLevel", "hasNonZeroStock", "coverage", "asOf", "contractVersion"],
            Enumerable.Range(0, 6).Select(i => current[$"components.schemas.StockPresence.required[{i}]"]));
        Assert.Equal(["asOf", "contractVersion", "coverage", "hasNonZeroStock", "skuId", "skuLevel"],
            current.Keys.Where(k => k.StartsWith("components.schemas.StockPresence.properties.", StringComparison.Ordinal))
                .Select(k => k.Split('.')[4]).Distinct().OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal(["COMPLETE", "PARTIAL"], Enumerable.Range(0, 2).Select(i => current[$"components.schemas.StockPresence.properties.coverage.enum[{i}]"]));

        var changelog = string.Join("\n", current.Where(kv => kv.Key.StartsWith("info.x-changelog[", StringComparison.Ordinal)).Select(kv => kv.Value));
        Assert.Contains("1.1.0 — additive (MVP-1 CT, MOD-0290 R-12 and PRODUCT-MASTER 1.1): SkuLevel + Item; getStockPresence", changelog);
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

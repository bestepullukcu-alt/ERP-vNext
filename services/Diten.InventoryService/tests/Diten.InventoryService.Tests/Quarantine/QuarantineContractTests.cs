using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers.Quarantine;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// WP-0175-Q1 freeze: <c>quarantine.openapi.yaml</c> 1.0.0 = the CT-accepted proposal (the "diff test"). Measured against the
/// PROPOSAL FILE: every operation (method + path) of §4, every operationId it names, every error code of §4.24, the ten
/// permissions of §3, the decision states of §2.6 / §5, the history actions of §2.3, the 35 signable-subject fields of §2.12
/// and the workflow template of §7. The reconciliations R-1…R-4 are named in the yaml changelog. The response bodies of the
/// implemented operations are validated against this file on every API test (ContractValidatingHandler, closed objects).
/// </summary>
public sealed class QuarantineContractTests
{
    private static readonly string ProposalPath = RepoPaths.Contract("quarantine-bundle-v1-proposal.md");

    private static readonly Lazy<JsonObject> Yaml = new(() =>
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(ContractSchemas.QuarantineContractPath));
        return (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(raw))!;
    });

    private static readonly Lazy<string> Proposal = new(() => File.ReadAllText(ProposalPath));

    private static string Section(string startHeading, string endHeading)
    {
        var text = Proposal.Value;
        var start = text.IndexOf(startHeading, StringComparison.Ordinal);
        var end = text.IndexOf(endHeading, start + 1, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"proposal section {startHeading} not found");
        return text[start..end];
    }

    private static HashSet<string> Enum(string schema)
        => Yaml.Value["components"]!["schemas"]![schema]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void The_contract_is_frozen_1_3_0_owned_by_MOD_0175()
    {
        var info = Yaml.Value["info"]!;
        Assert.Equal("1.3.0", info["version"]!.GetValue<string>());
        Assert.Equal("FROZEN", info["x-status"]!.GetValue<string>());
        Assert.Equal("MOD-0175", info["x-owner"]!.GetValue<string>());
        Assert.Equal("/api/quarantine", Yaml.Value["servers"]![0]!["url"]!.GetValue<string>());
        var changelog = string.Join(" ", info["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()));
        Assert.All(new[] { "R-1", "R-2", "R-3", "R-4", "1.1.0", "WP-0174-ITEM", "Item", "1.2.0", "WP-MVP1-FUB2", "NoteUpdated", "QUARANTINE_DECISION_ALREADY_PENDING", "1.3.0", "WP-0177-R3", "RECALL_CASE" },
            r => Assert.Contains(r, changelog, StringComparison.Ordinal));
    }

    private const string FrozenV10Sha256 = "c8f31720cd7f8b5d379160465c8c60d9f1dbc08c9f2548ad0775e1ebf60946a9";

    /// <summary>WP-MVP1-FUB2: the 1.1.0 file as frozen by WP-0174-ITEM (base <c>fa4c4e188</c>), kept byte for byte.</summary>
    private const string FrozenV11Sha256 = "31b21f0843c83434d77fe5745b8f657653c1c1444f64bef56225f14c3270b58a";

    private static string FixtureV10Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "quarantine-v1.0.0.openapi.yaml");

    private static string FixtureV11Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "quarantine-v1.1.0.openapi.yaml");

    /// <summary>WP-0177-R3: the 1.2.0 file as frozen by WP-MVP1-FUB2 (git blob at f35197c5a), kept byte for byte.</summary>
    private const string FrozenV12Sha256 = "b3fce0814a1c0cfa491ae9695991b66772bd3cd35b12f3acdece4bf48f6dff1d";

    private static string FixtureV12Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "quarantine-v1.2.0.openapi.yaml");

    [Fact]
    public void The_v1_2_fixture_is_the_1_2_0_file_byte_for_byte()
        => Assert.Equal(FrozenV12Sha256, Sha256(FixtureV12Path));

    /// <summary>
    /// WP-0177-R3 (MOD-0177 CT-R3, owner step MOD-0175): 1.3.0 = 1.2.0 + <c>Recall</c> appended to <c>HoldSource</c> + a description on
    /// <c>SourceDocument</c> and on <c>listHoldsBySource</c> (both new keys) + one changelog line + the version. Everything else is byte-equal as data.
    /// </summary>
    [Fact]
    public void The_1_3_0_change_is_Recall_on_HoldSource_and_two_descriptions_and_nothing_else()
    {
        var before = LoadJson(FixtureV12Path);
        var after = LoadJson(ContractSchemas.QuarantineContractPath);
        string[] Values(JsonNode? array) => array!.AsArray().Select(v => v!.GetValue<string>()).ToArray();
        Assert.Equal("1.3.0", after["info"]!["version"]!.GetValue<string>());
        Assert.Equal([.. Values(before["components"]!["schemas"]!["HoldSource"]!["enum"]), "Recall"], Values(after["components"]!["schemas"]!["HoldSource"]!["enum"]));
        Assert.Null(before["components"]!["schemas"]!["SourceDocument"]!["description"]);
        Assert.Contains("RECALL_CASE", after["components"]!["schemas"]!["SourceDocument"]!["description"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(before["paths"]!["/holds/by-source"]?["get"]?["description"]);
        var oldLog = Values(before["info"]!["x-changelog"]);
        var newLog = Values(after["info"]!["x-changelog"]);
        Assert.Equal(oldLog, newLog.Take(oldLog.Length));
        Assert.StartsWith("1.3.0 — additive (WP-0177-R3", Assert.Single(newLog.Skip(oldLog.Length)));
        Assert.Equal(Values(before["components"]!["schemas"]!["HoldSource"]!["enum"]).Length + 1,
            Enum<Diten.InventoryService.Domain.Quarantine.Enums.HoldSource>().Length); // the domain enum has exactly the wire values

        foreach (var file in new[] { before, after })
        {
            file["info"]!.AsObject().Remove("version");
            file["info"]!.AsObject().Remove("x-changelog");
            file["components"]!["schemas"]!["HoldSource"]!.AsObject().Remove("enum");
            file["components"]!["schemas"]!["SourceDocument"]!.AsObject().Remove("description");
            foreach (var path in file["paths"]!.AsObject().Where(x => x.Value?["get"]?["operationId"]?.GetValue<string>() == "listHoldsBySource").ToList())
            {
                path.Value!["get"]!.AsObject().Remove("description");
            }
        }

        Assert.Equal(before.ToJsonString(), after.ToJsonString());
    }

    private static string[] Enum<T>() where T : struct, System.Enum => System.Enum.GetNames<T>();

    private static string Sha256(string path)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n")))).ToLowerInvariant();

    [Fact]
    public void The_v1_1_fixture_is_the_1_1_0_file_byte_for_byte()
        => Assert.Equal(FrozenV11Sha256, Sha256(FixtureV11Path));

    /// <summary>
    /// WP-MVP1-FUB2: 1.2.0 = 1.1.0 + <c>NoteUpdated</c> appended to <c>HistoryAction</c> + the optional <c>HistoryEntry.noteChange</c>
    /// (<c>NoteChange</c>, a new schema) + <c>QUARANTINE_DECISION_ALREADY_PENDING</c> appended to updateHold's x-error-codes and its
    /// description + one changelog line + the version. Everything else is byte-equal as data.
    /// </summary>
    [Fact]
    public void The_1_2_0_change_is_the_note_history_row_and_the_note_lock_code_and_nothing_else()
    {
        var before = LoadJson(FixtureV11Path);
        var after = LoadJson(FixtureV12Path); // WP-0177-R3: the 1.2.0 file is now the fixture; 1.3.0 is measured against it
        string[] Values(JsonNode? array) => array!.AsArray().Select(v => v!.GetValue<string>()).ToArray();
        var actions = Values(before["components"]!["schemas"]!["HistoryAction"]!["enum"]);
        Assert.Equal([.. actions, "NoteUpdated"], Values(after["components"]!["schemas"]!["HistoryAction"]!["enum"]));
        var update = (JsonObject)after["paths"]!["/holds/{holdId}"]!["patch"]!;
        Assert.Equal([.. Values(before["paths"]!["/holds/{holdId}"]!["patch"]!["x-error-codes"]), "QUARANTINE_DECISION_ALREADY_PENDING"], Values(update["x-error-codes"]));
        Assert.Contains("QUARANTINE_DECISION_ALREADY_PENDING", update["description"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(before["paths"]!["/holds/{holdId}"]!["patch"]!["description"]);
        Assert.Equal("#/components/schemas/NoteChange", after["components"]!["schemas"]!["HistoryEntry"]!["properties"]!["noteChange"]!["$ref"]!.GetValue<string>());
        var noteChange = after["components"]!["schemas"]!["NoteChange"]!;
        Assert.Equal(["previousNoteLength", "noteLength", "previousAttachmentCount", "attachmentCount"], Values(noteChange["required"]));
        Assert.All(noteChange["properties"]!.AsObject(), p => Assert.Equal("integer", p.Value!["type"]!.GetValue<string>()));
        Assert.Null(before["components"]!["schemas"]!["NoteChange"]);
        Assert.Equal(["atUtc", "by", "action"], Values(after["components"]!["schemas"]!["HistoryEntry"]!["required"])); // noteChange is optional
        var oldLog = Values(before["info"]!["x-changelog"]);
        var newLog = Values(after["info"]!["x-changelog"]);
        Assert.Equal(oldLog, newLog.Take(oldLog.Length));
        Assert.Single(newLog.Skip(oldLog.Length));

        foreach (var file in new[] { before, after })
        {
            file["info"]!.AsObject().Remove("version");
            file["info"]!.AsObject().Remove("x-changelog");
            file["components"]!["schemas"]!["HistoryAction"]!.AsObject().Remove("enum");
            file["components"]!["schemas"]!["HistoryEntry"]!["properties"]!.AsObject().Remove("noteChange");
            file["components"]!["schemas"]!.AsObject().Remove("NoteChange");
            file["paths"]!["/holds/{holdId}"]!["patch"]!.AsObject().Remove("x-error-codes");
            file["paths"]!["/holds/{holdId}"]!["patch"]!.AsObject().Remove("description");
        }

        Assert.Equal(before.ToJsonString(), after.ToJsonString());
    }

    private static JsonObject LoadJson(string path)
        => (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    [Fact]
    public void The_v1_0_fixture_is_the_1_0_0_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixtureV10Path).Replace("\r\n", "\n");
        Assert.Equal(FrozenV10Sha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    /// <summary>
    /// WP-0174-ITEM: 1.1.0 = 1.0.0 + <c>Item</c> appended to <c>SkuLevel</c> + one changelog line + the version. Everything else of the
    /// file (paths, parameters, schemas, every other enum, required lists, status codes, the qd-v1 field list) is byte-equal as data.
    /// </summary>
    [Fact]
    public void The_1_1_0_change_is_Item_on_SkuLevel_and_nothing_else()
    {
        var before = LoadJson(FixtureV10Path);
        var after = LoadJson(FixtureV11Path); // WP-MVP1-FUB2: the 1.1.0 file is now the fixture; 1.2.0 is measured against it above
        Assert.Equal(["Gsku", "Lsku", "FinishedGood"], before["components"]!["schemas"]!["SkuLevel"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()));
        Assert.Equal(["Gsku", "Lsku", "FinishedGood", "Item"], after["components"]!["schemas"]!["SkuLevel"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()));
        var oldLog = before["info"]!["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        var newLog = after["info"]!["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        Assert.Equal(oldLog, newLog.Take(oldLog.Count));
        Assert.Single(newLog.Skip(oldLog.Count));

        foreach (var file in new[] { before, after })
        {
            file["info"]!.AsObject().Remove("version");
            file["info"]!.AsObject().Remove("x-changelog");
            file["components"]!["schemas"]!["SkuLevel"]!.AsObject().Remove("enum");
        }

        Assert.Equal(before.ToJsonString(), after.ToJsonString());
    }

    [Fact]
    public void Every_operation_of_the_proposal_is_in_the_contract_with_its_operationId()
    {
        var operations = Section("## 4. Operations", "### 4.24");
        var proposed = Regex.Matches(operations, @"^### 4\.\d+ .*$", RegexOptions.Multiline)
            .SelectMany(h => Regex.Matches(h.Value, @"`(GET|POST|PUT|PATCH) (/[^`?]+)[^`]*`").Select(m => $"{m.Groups[1].Value} {m.Groups[2].Value}"))
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(proposed.Count >= 22, $"parsed {proposed.Count} proposal operations");

        var contract = Yaml.Value["paths"]!.AsObject()
            .SelectMany(p => p.Value!.AsObject().Select(m => $"{m.Key.ToUpperInvariant()} {p.Key}")).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(proposed.OrderBy(x => x, StringComparer.Ordinal), contract.OrderBy(x => x, StringComparer.Ordinal));

        var proposedIds = Regex.Matches(operations, @"^### 4\.\d+ .*$", RegexOptions.Multiline)
            .SelectMany(h => Regex.Matches(h.Value, @"(?:—|/) `([a-z][A-Za-z]+)`").Select(m => m.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);
        var contractIds = Yaml.Value["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject().Select(m => m.Value!["operationId"]!.GetValue<string>()))
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(proposedIds.Count >= 20);
        Assert.All(proposedIds, id => Assert.Contains(id, contractIds));
        Assert.Equal(contractIds.Count, contractIds.Count(id => id.Length > 0));
    }

    [Fact]
    public void Every_error_code_permission_state_and_history_action_of_the_proposal_is_in_the_contract()
    {
        var codes = Regex.Matches(Section("### 4.24", "## 5."), @"`([A-Z][A-Z_]+)`").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.True(codes.Count >= 35, $"parsed {codes.Count} codes");
        var yamlCodes = string.Join(" ", Yaml.Value["x-error-codes"]!.AsObject().Select(e => e.Key));
        Assert.All(codes, c => Assert.Contains(c, yamlCodes, StringComparison.Ordinal));
        // the shorthands of the proposal table (QUARANTINE_DECISION_NOT_DRAFT / _NOT_PENDING / _NOT_APPLICABLE …) expand to full codes
        Assert.All(new[] { "QUARANTINE_DECISION_NOT_PENDING", "QUARANTINE_DECISION_NOT_APPLICABLE", "QUARANTINE_SAMPLE_LOCATION_INVALID",
            "QUARANTINE_BLOCK_REASON_EXISTS", "QUARANTINE_BLOCK_REASON_IN_USE", "QUARANTINE_BLOCKED_CLOSED", "QUARANTINE_BLOCKED_NOT_FOUND", "QUARANTINE_DECISION_NOT_FOUND" },
            c => Assert.True(Yaml.Value["x-error-codes"]![c] is not null, c));

        var permissions = Regex.Matches(Section("## 3. Permissions", "Outside keys"), @"`(quarantine\.[a-z.\-]+)`").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(10, permissions.Count);
        Assert.Equal(permissions.OrderBy(x => x), Yaml.Value["x-permissions"]!["owned"]!.AsArray().Select(v => v!.GetValue<string>()).OrderBy(x => x));
        Assert.Equal(permissions.OrderBy(x => x), QuarantinePermissions.All.OrderBy(x => x));

        var states = Regex.Match(Section("### 2.6", "### 2.7"), @"""state"": ""([^""]+)""").Groups[1].Value.Split('|').Select(s => s.Trim()).ToHashSet();
        Assert.Equal(12, states.Count);
        Assert.Equal(states.OrderBy(x => x), Enum("DecisionState").OrderBy(x => x));
        Assert.Equal(states.OrderBy(x => x), System.Enum.GetNames<DecisionState>().OrderBy(x => x));

        var actions = Regex.Match(Section("### 2.3", "### 2.4"), @"""action"": ""([^""]+)""").Groups[1].Value.Split('|').Select(s => s.Trim()).ToHashSet();
        actions.Add("NoteUpdated"); // QUARANTINE 1.2.0 (WP-MVP1-FUB2), after the proposal; measured by The_1_2_0_change_is_…
        Assert.Equal(actions.OrderBy(x => x), Enum("HistoryAction").OrderBy(x => x));
        Assert.Equal(actions.OrderBy(x => x), System.Enum.GetNames<HistoryAction>().OrderBy(x => x));
    }

    [Fact]
    public void The_signable_subject_fields_and_the_workflow_template_are_the_proposals()
    {
        var subject = Section("### 2.12", "## 3.");
        var fields = Regex.Match(subject, @"""fields"": \{(?<f>[\s\S]*?)\} \}").Groups["f"].Value;
        var proposedFields = Regex.Matches(fields, @"""([A-Za-z0-9.]+)"":").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(35, proposedFields.Count);
        var contractFields = Yaml.Value["components"]!["schemas"]!["SignableSubject"]!["properties"]!["fields"]!["required"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        Assert.Equal(proposedFields, contractFields);

        var proposedTemplate = Regex.Match(Section("## 7.", "## 8."), @"```json\s*(?<j>[\s\S]*?)```").Groups["j"].Value;
        var contractTemplate = Yaml.Value["x-workflow-template"]!["definition"]!.GetValue<string>();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(proposedTemplate), JsonNode.Parse(contractTemplate)), "workflow template differs from proposal §7");
    }

    /// <summary>Q2: the template the code publishes ("Onay şablonunu hazırla") is the contract's <c>x-workflow-template</c>.</summary>
    [Fact]
    public void The_shipped_workflow_template_is_the_contracts_x_workflow_template()
    {
        var contract = Yaml.Value["x-workflow-template"]!;
        Assert.Equal(Diten.InventoryService.Application.Features.Quarantine.Ports.QuarantineWorkflowTemplate.Code, contract["code"]!.GetValue<string>());
        Assert.Equal(Diten.InventoryService.Application.Features.Quarantine.Ports.QuarantineWorkflowTemplate.SchemaVersion, contract["schemaVersion"]!.GetValue<string>());
        Assert.Equal(Diten.InventoryService.Application.Features.Quarantine.Ports.QuarantineWorkflowTemplate.ExpressionVersion, contract["expressionVersion"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(contract["definition"]!.GetValue<string>()),
            JsonNode.Parse(Diten.InventoryService.Application.Features.Quarantine.Ports.QuarantineWorkflowTemplate.DefinitionJson)));
    }

    [Fact]
    public void Every_route_slices_Q1_to_Q5_serve_is_an_operation_of_the_contract()
    {
        var contract = Yaml.Value["paths"]!.AsObject()
            .SelectMany(p => p.Value!.AsObject().Select(m => $"{m.Key.ToUpperInvariant()} {Regex.Replace(p.Key, @"\{[^}]+\}", "{}")}")).ToHashSet(StringComparer.Ordinal);
        var served = typeof(QuarantineController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods.Select(v => $"{v} /{Regex.Replace(a.Template ?? "", @"\{[^}]+\}", "{}")}")).ToList();
        // Q1 17 + Q2 refresh, withdraw, workflow-template + Q4 listRetest + Q5 getLotQualityStatus + WP-0174-ITEM updateHold (FU-0175-Q5-01)
        Assert.Equal(23, served.Count);
        Assert.Contains("PATCH /holds/{}", served);
        Assert.All(served, s => Assert.Contains(s, contract));
    }
}

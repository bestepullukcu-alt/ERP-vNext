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
    public void The_contract_is_frozen_1_0_0_owned_by_MOD_0175()
    {
        var info = Yaml.Value["info"]!;
        Assert.Equal("1.0.0", info["version"]!.GetValue<string>());
        Assert.Equal("FROZEN", info["x-status"]!.GetValue<string>());
        Assert.Equal("MOD-0175", info["x-owner"]!.GetValue<string>());
        Assert.Equal("/api/quarantine", Yaml.Value["servers"]![0]!["url"]!.GetValue<string>());
        var changelog = string.Join(" ", info["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()));
        Assert.All(new[] { "R-1", "R-2", "R-3", "R-4" }, r => Assert.Contains(r, changelog, StringComparison.Ordinal));
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

    [Fact]
    public void Every_route_slice_Q1_serves_is_an_operation_of_the_contract()
    {
        var contract = Yaml.Value["paths"]!.AsObject()
            .SelectMany(p => p.Value!.AsObject().Select(m => $"{m.Key.ToUpperInvariant()} {Regex.Replace(p.Key, @"\{[^}]+\}", "{}")}")).ToHashSet(StringComparer.Ordinal);
        var served = typeof(QuarantineController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods.Select(v => $"{v} /{Regex.Replace(a.Template ?? "", @"\{[^}]+\}", "{}")}")).ToList();
        Assert.Equal(17, served.Count);
        Assert.All(served, s => Assert.Contains(s, contract));
    }
}

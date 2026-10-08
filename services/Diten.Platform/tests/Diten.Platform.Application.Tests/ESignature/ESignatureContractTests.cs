using System.Reflection;
using System.Text.RegularExpressions;
using Diten.Platform.API.Controllers.ESignature;
using Diten.Platform.API.Controllers.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E2 — the frozen contract equals the CT-accepted proposal, and the code serves exactly the contract's E2 surface.
///
/// <para>Both files are FOUND BY NAME under the repository's docs folder rather than by a typed path: DocsPathGuard forbids a
/// code file from naming a docs sub-folder outside the five, and a name search keeps working when documents move — the very
/// breakage that guard exists for. If either file is missing the test fails; it never skips.</para>
/// </summary>
public sealed class ESignatureContractTests
{
    private const string ContractFile = "esign.openapi.yaml";
    private const string ProposalFile = "esign-bundle-v1-proposal.md";

    private static readonly Regex Operation = new(@"\b(GET|POST|PUT|PATCH|DELETE) (/[A-Za-z0-9/{}_.-]+)", RegexOptions.Compiled);

    [Fact]
    public void The_contract_is_frozen_1_0_0_and_owned_by_MOD_0022()
    {
        var yaml = Read(ContractFile);
        Assert.Matches(@"(?m)^  version: 1\.0\.0$", yaml);
        Assert.Matches(@"(?m)^  x-status: FROZEN$", yaml);
        Assert.Matches(@"(?m)^  x-owner: MOD-0022$", yaml);
    }

    [Fact]
    public void Every_operation_of_the_proposal_is_in_the_contract_and_nothing_else()
    {
        var proposal = ProposalOperations();
        var contract = ContractOperations();
        Assert.True(proposal.Count >= 30, $"the proposal scan found only {proposal.Count} operations");

        var missing = proposal.Except(contract).OrderBy(x => x).ToList();
        var extra = contract.Except(proposal).OrderBy(x => x).ToList();
        Assert.True(missing.Count == 0, "operations of the proposal missing from the contract: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "operations in the contract the proposal does not have: " + string.Join(", ", extra));
    }

    [Fact]
    public void Every_named_operation_schema_and_error_code_of_the_proposal_is_in_the_contract()
    {
        var proposal = Read(ProposalFile);
        var yaml = Read(ContractFile);
        var operationIds = Regex.Matches(yaml, @"(?m)^      operationId: (\w+)$").Select(m => m.Groups[1].Value).ToHashSet();
        var schemas = SectionKeys(yaml, "  schemas:", 4);

        var namedOperations = Regex.Matches(Section(proposal, "## 4.", "### 4.27"), @"(?m)^### 4\.\d+ .*$")
            .SelectMany(heading => Regex.Matches(heading.Value.Split('—').Last(), @"`([a-z][A-Za-z]+)`").Select(m => m.Groups[1].Value))
            .ToList();
        var proposalSchemas = Regex.Matches(proposal, @"(?m)^### 2\.\d+ `(\w+)`").Select(m => m.Groups[1].Value).ToList();
        var proposalCodes = Regex.Matches(Section(proposal, "### 4.27", "## 5."), @"(?m)^\| `(ESIGN_[A-Z_]+)` \|").Select(m => m.Groups[1].Value).ToList();
        var contractCodes = Regex.Matches(Section(yaml, "    ErrorCode:", "      x-http:"), @"(?m)^        - (ESIGN_[A-Z_]+)$").Select(m => m.Groups[1].Value).ToList();

        Assert.True(namedOperations.Count >= 30 && proposalSchemas.Count == 12 && proposalCodes.Count >= 30, "proposal scan collapsed");
        Assert.Empty(namedOperations.Except(operationIds));
        Assert.Empty(proposalSchemas.Except(schemas));
        Assert.Equal(proposalCodes.OrderBy(c => c), contractCodes.OrderBy(c => c));
    }

    [Fact]
    public void The_E2_controllers_serve_exactly_the_contracts_E2_operations()
    {
        var served = typeof(ESignatureSignaturesController).Assembly.GetTypes()
            .Where(t => t == typeof(InternalESignActionsController)
                        || (t.Namespace == typeof(ESignatureSignaturesController).Namespace && typeof(ControllerBase).IsAssignableFrom(t)))
            .SelectMany(Routes)
            .ToHashSet();

        var e2 = ContractOperationsBySlice("E2");
        Assert.True(e2.Count >= 25, $"contract scan found only {e2.Count} E2 operations");
        Assert.Empty(e2.Except(served).OrderBy(x => x));
        Assert.Empty(served.Except(e2).OrderBy(x => x));
    }

    [Fact]
    public void The_golden_vectors_in_the_contract_are_the_ones_the_code_produces()
    {
        var yaml = Read(ContractFile);
        Assert.Contains($"    subjectHash: {ESignatureRulesTests.GoldenSubjectHash}", yaml, StringComparison.Ordinal);
        Assert.Contains($"    signatureDigest: {ESignatureRulesTests.GoldenSignatureDigest}", yaml, StringComparison.Ordinal);
    }

    // ── scanning ─────────────────────────────────────────────────────────────────────────────────────────────────────

    private static HashSet<string> ProposalOperations()
    {
        var operations = Section(Read(ProposalFile), "## 4. Operations", "### 4.27");
        return Operation.Matches(operations).Select(m => $"{m.Groups[1].Value} {m.Groups[2].Value.TrimEnd('.')}").ToHashSet();
    }

    private static HashSet<string> ContractOperations() => ContractOperationsBySlice(null);

    /// <summary>"METHOD /path" for each operation in the paths section (optionally only those whose x-slice matches).</summary>
    private static HashSet<string> ContractOperationsBySlice(string? slice)
    {
        var lines = Section(Read(ContractFile), "paths:", "components:").Split('\n');
        var result = new HashSet<string>();
        string? path = null;
        string? method = null;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            var pathMatch = Regex.Match(line, @"^  (/\S+):$");
            if (pathMatch.Success)
            {
                path = pathMatch.Groups[1].Value;
                continue;
            }

            var methodMatch = Regex.Match(line, @"^    (get|post|put|patch|delete):$");
            if (methodMatch.Success && path is not null)
            {
                method = methodMatch.Groups[1].Value.ToUpperInvariant();
                if (slice is null)
                {
                    result.Add($"{method} {path}");
                }

                continue;
            }

            var sliceMatch = Regex.Match(line, @"^      x-slice: (\w+)$");
            if (sliceMatch.Success && slice is not null && sliceMatch.Groups[1].Value == slice && method is not null)
            {
                result.Add($"{method} {path}");
            }
        }

        return result;
    }

    private static IEnumerable<string> Routes(Type controller)
    {
        var prefix = controller.GetCustomAttribute<RouteAttribute>()!.Template.Trim('/');
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
            {
                var template = string.IsNullOrEmpty(http.Template) ? prefix : $"{prefix}/{http.Template}";
                var normalized = Regex.Replace("/" + template, @"\{(\w+):[^}]+\}", "{$1}");
                foreach (var method in http.HttpMethods)
                {
                    yield return $"{method} {normalized}";
                }
            }
        }
    }

    private static HashSet<string> SectionKeys(string text, string header, int indent)
    {
        var body = text[(text.IndexOf("\n" + header, StringComparison.Ordinal) + header.Length + 1)..];
        var keys = new HashSet<string>();
        var pad = new string(' ', indent);
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length > 0 && !line.StartsWith(" ", StringComparison.Ordinal))
            {
                break; // next top-level key
            }

            var match = Regex.Match(line, $"^{pad}([A-Za-z][A-Za-z0-9]*):");
            if (match.Success)
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    private static string Section(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"section '{from}' not found");
        var end = text.IndexOf(to, start + from.Length, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static string Read(string fileName)
    {
        var docs = Path.Combine(RepoRoot(), "docs");
        var matches = Directory.EnumerateFiles(docs, fileName, SearchOption.AllDirectories).ToList();
        Assert.True(matches.Count == 1, $"expected exactly one {fileName} under the docs folder, found {matches.Count}");
        return File.ReadAllText(matches[0]);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository root (AGENTS.md) not found");
    }
}

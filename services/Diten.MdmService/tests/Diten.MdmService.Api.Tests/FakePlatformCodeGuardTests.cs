using System.Text.RegularExpressions;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX4 / FIX5 / FIX6 item 5 (b): the fake engine answers only what the real chain can answer.
/// FIX3 made the fake answer WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION before Platform did (BL-547), and the tests went
/// green on behaviour production never saw. Two rules, both read from source (Platform is read, never changed):
///   1. every code the fake answers as an ENGINE answer (an envelope from Platform) is answered by a Platform handler,
///      coordinator or service — not merely declared — and next to the HTTP status the fake's outcome stands for
///      (Conflict / NonTerminal → 409, NotFound → 404, Forbidden → 403, AuthenticationRejected → 401, Invalid → 400);
///   2. every code a test feeds into the fake (a start or lookup failure) is either such an engine answer or one the
///      real client itself reports (PlatformProductIdentityWorkflowClient / ProductIdentityWorkflowFaultPolicy, e.g.
///      PRODUCT_WORKFLOW_TIMEOUT) — test-only codes ("WORKFLOW_TIMEOUT") are not allowed.
/// FAKE_ENGINE_* codes stand for a transport failure the fake simulates without an envelope and are exempt.
/// </summary>
public sealed class FakePlatformCodeGuardTests
{
    private static readonly Regex FakeAnswer = new(
        @"ProductIdentityWorkflowTransportOutcome\.(\w+),\s*""((?:WORKFLOW|NOT_FOUND)[A-Z0-9_]*)""", RegexOptions.CultureInvariant);
    private static readonly Regex FedCode = new(
        @"(?:NextStartFailure\s*=\s*\(\s*ProductIdentityWorkflowTransportOutcome\.\w+,\s*|LookupFailureCode\s*=\s*)""([A-Z][A-Z0-9_]+)""",
        RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, string> StatusFor = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Conflict"] = "409", ["NonTerminal"] = "409", ["Incomplete"] = "409", ["NotFound"] = "404",
        ["Forbidden"] = "403", ["AuthenticationRejected"] = "401", ["Invalid"] = "400"
    };

    [Fact]
    public void Every_code_the_fake_engine_answers_is_one_a_Platform_handler_answers_with_its_status()
    {
        var fake = Read("services", "Diten.MdmService", "tests", "Diten.MdmService.Api.Tests", "FakePlatform.cs");
        var answers = FakeAnswer.Matches(fake).Select(m => (Outcome: m.Groups[1].Value, Code: m.Groups[2].Value))
            .Distinct().ToArray();
        Assert.NotEmpty(answers);
        var platform = PlatformSources();

        var unanswered = answers
            .Where(answer => !IsAnswered(answer.Code, StatusFor.GetValueOrDefault(answer.Outcome, "4"), platform))
            .Select(answer => $"{answer.Code} ({answer.Outcome})")
            .ToArray();

        Assert.True(unanswered.Length == 0,
            "The fake engine answers codes no Platform handler answers with that status: " + string.Join(", ", unanswered));
    }

    [Fact]
    public void Every_code_a_test_feeds_into_the_fake_engine_is_one_the_real_chain_can_answer()
    {
        var tests = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "services", "Diten.MdmService", "tests",
                "Diten.MdmService.Api.Tests"), "*.cs")
            .Select(File.ReadAllText);
        // The real client and its service-identity provider (Infrastructure/Workflow), and the shared fault policy.
        var client = string.Concat(Directory.EnumerateFiles(Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src",
                         "Diten.MdmService.Infrastructure", "Workflow"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText))
                     + Read("services", "Diten.MdmService", "src", "Diten.MdmService.Application", "Features",
                         "ProductItemSkuMaster", "Workflow", "ProductIdentityWorkflowFaultPolicy.cs");
        var platform = PlatformSources();
        var fed = tests.SelectMany(source => FedCode.Matches(source).Select(m => m.Groups[1].Value))
            .Where(code => !code.StartsWith("FAKE_ENGINE_", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(fed);

        var unknown = fed
            .Where(code => !client.Contains($"\"{code}\"", StringComparison.Ordinal) && !IsAnswered(code, "4", platform))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.True(unknown.Length == 0, "Tests feed the fake engine codes the real chain never answers: " + string.Join(", ", unknown));
    }

    // Answered: the code (its literal, or a constant that holds it) appears in a Platform file OTHER than the one that
    // declares the constant, within three lines of the expected HTTP status ("4" = any 4xx when only a family is known).
    private static bool IsAnswered(string code, string status, IReadOnlyList<(string Path, string[] Lines)> platform)
    {
        var declaration = new Regex($@"const\s+string\s+(\w+)\s*=\s*""{Regex.Escape(code)}""", RegexOptions.CultureInvariant);
        var declaring = platform.Where(file => file.Lines.Any(declaration.IsMatch)).Select(file => file.Path).ToHashSet();
        var names = platform.SelectMany(file => file.Lines.Select(line => declaration.Match(line)))
            .Where(match => match.Success).Select(match => match.Groups[1].Value).Distinct().ToArray();
        var reference = new Regex(
            $@"""{Regex.Escape(code)}""" + (names.Length == 0 ? "" : "|" + string.Join("|", names.Select(n => $@"\.{Regex.Escape(n)}\b"))),
            RegexOptions.CultureInvariant);
        // The status as a number, a StatusCodes constant, or a helper that names it (Conflict(...) is Platform's 409, …).
        var helper = status switch
        {
            "409" => @"|\bConflict\w*\(|\bInconsistent\(|\bNotTerminal\(|\bClosedWithoutDecision\(",
            "404" => @"|\bNotFound\w*\(",
            "403" => @"|\bForbidden\w*\(",
            "401" => @"|\bUnauthori[sz]ed\w*\(",
            "400" => @"|\bBadRequest\w*\(|\bValidation\w*\(",
            _ => @"|\b(?:Conflict|NotFound|Forbidden|Unauthori[sz]ed|BadRequest|Inconsistent|NotTerminal)\w*\("
        };
        var statusMark = new Regex($@"\b{Regex.Escape(status)}\d{{0,2}}\b|Status{Regex.Escape(status)}{helper}", RegexOptions.CultureInvariant);
        foreach (var file in platform.Where(file => !declaring.Contains(file.Path)))
        {
            for (var i = 0; i < file.Lines.Length; i++)
            {
                if (!reference.IsMatch(file.Lines[i])) continue;
                var from = Math.Max(0, i - 3);
                var to = Math.Min(file.Lines.Length - 1, i + 3);
                for (var j = from; j <= to; j++)
                    if (statusMark.IsMatch(file.Lines[j])) return true;
            }
        }
        return false;
    }

    private static IReadOnlyList<(string Path, string[] Lines)> PlatformSources() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "services", "Diten.Platform", "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => (Path: path, Lines: File.ReadAllLines(path)))
            .ToArray();

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([RepoRoot(), .. parts]));

    private static string RepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
    }
}

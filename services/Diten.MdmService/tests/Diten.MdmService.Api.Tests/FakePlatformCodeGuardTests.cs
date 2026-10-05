using System.Reflection;
using System.Text.RegularExpressions;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX4 (CT decision 3 corrected): the fake engine answers only with codes the real engine has.
/// FIX3 made the fake answer WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION — a code Platform does not emit yet (BL-547) —
/// and the tests went green on behaviour production never sees. Every workflow code the fake answers, as a literal or
/// through a constant of ProductIdentityWorkflowFaultPolicy, must be defined in Platform's own source (read, not
/// changed). The fake's FAKE_ENGINE_* codes stand for a transport failure with no envelope and are not engine codes.
/// FIX5 item 1: DEFINED is not enough — WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION was a constant in Platform long before
/// any handler answered it. Each code must also be ANSWERED: its literal, or the constant that holds it, appears in a
/// Platform file other than the one that declares it (a handler, a coordinator, a service).
/// </summary>
public sealed class FakePlatformCodeGuardTests
{
    private static readonly Regex CodeLiteral = new("\"((?:WORKFLOW|NOT_FOUND)[A-Z0-9_]*)\"", RegexOptions.CultureInvariant);
    private static readonly Regex PolicyConstant = new(@"ProductIdentityWorkflowFaultPolicy\.([A-Za-z]+)\b", RegexOptions.CultureInvariant);

    [Fact]
    public void Every_code_the_fake_engine_answers_is_one_Platform_defines()
    {
        var fake = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.MdmService", "tests",
            "Diten.MdmService.Api.Tests", "FakePlatform.cs"));
        var codes = CodeLiteral.Matches(fake).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        foreach (Match match in PolicyConstant.Matches(fake))
        {
            var field = typeof(ProductIdentityWorkflowFaultPolicy).GetField(match.Groups[1].Value,
                BindingFlags.Public | BindingFlags.Static);
            if (field?.GetRawConstantValue() is string value && value.StartsWith("WORKFLOW_", StringComparison.Ordinal))
                codes.Add(value);
        }
        Assert.NotEmpty(codes);

        var platform = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "services", "Diten.Platform", "src"), "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .ToArray();
        var undefined = codes.Where(code => !platform.Any(file => file.Source.Contains($"\"{code}\"", StringComparison.Ordinal)))
            .OrderBy(code => code, StringComparer.Ordinal).ToArray();
        Assert.True(undefined.Length == 0, "The fake engine answers codes Platform does not define: " + string.Join(", ", undefined));

        var unanswered = codes.Where(code => !IsAnswered(code, platform)).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        Assert.True(unanswered.Length == 0, "The fake engine answers codes no Platform handler answers: " + string.Join(", ", unanswered));
    }

    private static bool IsAnswered(string code, IReadOnlyList<(string Path, string Source)> platform)
    {
        var declaration = new Regex($@"const\s+string\s+(\w+)\s*=\s*""{Regex.Escape(code)}""", RegexOptions.CultureInvariant);
        var declaringFiles = platform.Where(file => declaration.IsMatch(file.Source)).Select(file => file.Path).ToHashSet();
        var names = platform.SelectMany(file => declaration.Matches(file.Source).Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal).ToArray();
        return platform.Where(file => !declaringFiles.Contains(file.Path)).Any(file =>
            file.Source.Contains($"\"{code}\"", StringComparison.Ordinal)
            || names.Any(name => Regex.IsMatch(file.Source, $@"\.{Regex.Escape(name)}\b")));
    }

    private static string RepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
    }
}

using System.Text.RegularExpressions;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-ABB-ACCEPT-01 FIX1 S4 — every refusal code the ABB register can send to its page is mapped there to a
/// sentence. Both sides are read from production files: MDM's ABB sources (the codes) and the page script's
/// ERROR_KEYS map. The seven-language key parity of those sentences is the page's own test
/// (frontend/Diten.Web/tests/abb-register-error-sentences.test.js).
/// </summary>
public sealed class ProductAbbreviationErrorSentenceContractTests
{
    // Projection-only codes: they never reach the register page (the projection logs and skips the row).
    private static readonly HashSet<string> ProjectionOnly = new(StringComparer.Ordinal)
    {
        "ABBREVIATION_WORK_ITEM_SOURCE_INCONSISTENT",
        "ABBREVIATION_WORK_ITEM_PRODUCT_MISSING",
        "ABBREVIATION_WORK_ITEM_BOUND_EXCEEDED"
    };

    [Fact]
    public void Every_code_the_register_answers_has_a_sentence_on_the_page()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md")))
            root = root.Parent;
        Assert.NotNull(root);
        var src = Path.Combine(root.FullName, "services", "Diten.MdmService", "src");
        var sources = Directory.EnumerateFiles(
                Path.Combine(src, "Diten.MdmService.Application", "Features", "ProductAbbreviationRegister"), "*.cs",
                SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(src, "Diten.MdmService.Persistence", "Repositories"),
                "ProductAbbreviation*.cs"))
            .Select(File.ReadAllText);
        var emitted = sources
            .SelectMany(text => Regex.Matches(text,
                "\"((?:ABBREVIATION|ACTIVE_ABBREVIATION|PRODUCT_ABBREVIATION)_[A-Z_]+|GLOBAL_PRODUCT_NOT_FOUND|CONCURRENCY_CONFLICT)\""))
            .Select(match => match.Groups[1].Value)
            .Where(code => !ProjectionOnly.Contains(code))
            .ToHashSet(StringComparer.Ordinal);
        var script = File.ReadAllText(Path.Combine(root.FullName, "frontend", "Diten.Web", "wwwroot", "assets", "js",
            "MDM", "ProductAbbreviationRegister", "index.js"));
        var start = script.IndexOf("const ERROR_KEYS = Object.freeze({", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var block = script[start..script.IndexOf("});", start, StringComparison.Ordinal)];
        var mapped = Regex.Matches(block, @"^\s*([A-Z_]+): '[A-Za-z]+',?$", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(emitted.Count > 25, $"only {emitted.Count} codes found — the scan is not reading the sources");
        Assert.Empty(emitted.Except(mapped).Order());
    }
}

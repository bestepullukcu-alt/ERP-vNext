using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Services.Navigation;
using Xunit;

namespace Diten.Web.Tests.Navigation;

// FEAT-NAV-L10N-GUARD — every name the tenant sidebar can print must have a resx key in all SEVEN tenant
// languages. Measured 2026-08-10: three separate live defects of exactly this shape shipped green
//   1. an action label whose key existed in NO resx — ASP.NET's localizer returns the KEY NAME for a missing
//      resource, which is a non-empty string, so the screen showed "Edit" in every language and nothing failed;
//   2. TASK_RECURRENCE_RULES had no Nav.Page key — the menu fell back to the raw English manifest DisplayName
//      while its sibling TASK_FIELD_DEFINITIONS was fine, so the section looked half-translated;
//   3. the `tasks` module had no Nav.Module key — someone had worked around it by typing two languages into the
//      manifest DisplayName ("Görevler / Tasks").
// The old guard (Diten.Platform.Application.Tests/Navigation/NavL10nContractTests) could not catch any of them:
// its module and page lists were TYPED BY HAND, so a code nobody remembered to add was simply not asserted. This
// guard derives the whole expected set FROM THE MANIFEST PROVIDER SOURCE, so a new module or a new nav-visible
// page is covered the moment it is written — nobody has to remember this file.
//
// WHY IT LIVES HERE (frontend test project) rather than next to the manifests: the key transform must come from
// ONE place. This project references Diten.Web, so the test calls the shipping bridge's own
// NavNameLocalizer.Normalize — the exact method the runtime uses to turn TASK_RECURRENCE_RULES into
// Nav.Page.TASKRECURRENCERULES. A second copy of that transform in the test would drift and re-create the very
// defect being guarded (K6: a fact that lives in two places slides apart silently).
//
// LIMIT, stated plainly: this fails the TEST RUN, not the compiler. A missing resx key cannot be made a C#
// compile error — resx keys are resolved by string at runtime. CI runs dotnet test, so a missing key stops the
// pipeline; it does not stop `dotnet build`.
public sealed class NavManifestL10nGuardTests
{
    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // Measured against the manifests on disk. Lower it only when a nav-visible page is genuinely deleted, in the
    // same commit that deletes it — a drop that nobody chose is the parser regressing, and it must be red.
    private const int NavVisiblePageKeyFloor = 45;

    // ── the guard itself ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_manifest_module_has_a_Nav_Module_key_in_all_seven_languages()
    {
        var manifests = ReadManifests();
        var expected = manifests
            .Select(m => "Nav.Module." + NavNameLocalizer.Normalize(m.ModuleCode))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // One provider file declares exactly one module, so the floor tracks the repo with no constant to maintain.
        AssertKeysUsableInEveryLanguage(expected, manifests.Count);
    }

    [Fact]
    public void Every_nav_visible_manifest_page_has_a_Nav_Page_key_in_all_seven_languages()
    {
        var manifests = ReadManifests();
        var expected = manifests
            .SelectMany(m => m.NavVisiblePageCodes)
            .Select(code => "Nav.Page." + NavNameLocalizer.Normalize(code))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        AssertKeysUsableInEveryLanguage(expected, NavVisiblePageKeyFloor);
    }

    [Fact]
    public void Every_manifest_domain_has_a_Nav_Domain_key_in_all_seven_languages()
    {
        var manifests = ReadManifests();
        var expected = manifests
            .Select(m => "Nav.Domain." + NavNameLocalizer.Normalize(m.Domain))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Counted straight out of the raw sources, bypassing the page parser entirely: if the parser ever stops
        // understanding a provider, these two numbers disagree and the floor turns this red.
        AssertKeysUsableInEveryLanguage(expected, DistinctDomainCountFromRawSources());
    }

    // A key is only USABLE if it is present, non-empty, and not just an echo of its own name. Case 1 above is
    // exactly the echo: the localizer hands back the key name for a missing resource and the caller's
    // "is it non-empty?" check passes. A resx row whose value literally repeats the key is the same defect
    // written down, so it is rejected here too.
    private static void AssertKeysUsableInEveryLanguage(IReadOnlyCollection<string> expectedKeys, int minimumExpected)
    {
        /*
         * THE FLOOR IS A MEASURED NUMBER, NOT "> 0".
         *
         * A vacuity guard of Assert.NotEmpty is the failure mode this guard exists to prevent, one level up: if
         * the source parser regresses and understands ONE manifest out of fifteen, the expectation set shrinks to
         * a single key that happens to be translated, and the test reports green over fourteen unchecked modules.
         * That is not hypothetical — a guard in this repo went green on exactly one key this way.
         *
         * So each caller passes a floor measured against the manifests actually on disk. Two of the three are
         * derived live (module keys = one per provider file; domain keys are counted from the raw sources by a
         * regex that does NOT go through the page parser), so they track the repo by themselves. The page floor
         * is a constant, because nav-visibility is declared in two argument shapes and any independent counter
         * would just be a second copy of the parser. Deleting a nav-visible page is allowed — it means editing
         * NavVisiblePageKeyFloor by hand, deliberately, in the same commit.
         */
        Assert.True(expectedKeys.Count >= minimumExpected,
            $"expected at least {minimumExpected} keys to check but derived only {expectedKeys.Count} "
            + $"({string.Join(", ", expectedKeys.OrderBy(k => k, StringComparer.Ordinal))}). The manifest parser has "
            + "regressed: the guard would now report green over the manifests it stopped seeing.");

        var failures = new List<string>();
        foreach (var language in SupportedLanguages)
        {
            var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", $"SharedResource.{language}.resx");
            var rows = ResxRows(path);

            foreach (var key in expectedKeys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!rows.TryGetValue(key, out var value))
                {
                    failures.Add($"{language}: MISSING  {key}");
                }
                else if (string.IsNullOrWhiteSpace(value))
                {
                    failures.Add($"{language}: EMPTY    {key}");
                }
                else if (string.Equals(value.Trim(), key, StringComparison.Ordinal)
                         || string.Equals(value.Trim(), key[(key.LastIndexOf('.') + 1)..], StringComparison.Ordinal))
                {
                    failures.Add($"{language}: ECHOES THE KEY  {key} = \"{value}\"");
                }
            }
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} nav l10n key problem(s) — the sidebar would print raw English for these:\n  "
            + string.Join("\n  ", failures));
    }

    // ── the parser's own guards ─────────────────────────────────────────────────────────────────────────────
    // A source parser that silently skips what it cannot read is worse than no test: it reports green over the
    // exact codes it failed to see. These pin it down.

    [Fact]
    public void Every_manifest_provider_in_the_repo_is_parsed_and_yields_a_module_code_and_pages()
    {
        var manifests = ReadManifests();

        Assert.NotEmpty(manifests);
        foreach (var manifest in manifests)
        {
            Assert.False(string.IsNullOrWhiteSpace(manifest.ModuleCode), $"{manifest.File}: no ModuleCode parsed");
            Assert.False(string.IsNullOrWhiteSpace(manifest.Domain), $"{manifest.File}: no Domain parsed");
            Assert.True(manifest.ParsedPageCount > 0, $"{manifest.File}: no pages parsed");
            // Every `new ModuleManifestPage(` in the file must have been understood — named args, positional args
            // and const-referenced page codes are all in use today, and a fourth shape must fail LOUDLY here
            // rather than quietly shrink the expected key set.
            Assert.Equal(manifest.RawPageOccurrences, manifest.ParsedPageCount);
        }
    }

    [Fact]
    public void The_parser_sees_all_three_declaration_shapes_used_today()
    {
        var manifests = ReadManifests();

        // named args + const-referenced page code (TaskManifestProvider)
        var tasks = Single(manifests, "TaskManifestProvider.cs");
        Assert.Equal("tasks", tasks.ModuleCode);
        // All FOUR nav-visible Tasks pages, not a sample: they are the whole of what the "Görev Tanımları" module
        // shows in the sidebar, and every one of them was measured untranslated at some point.
        Assert.Contains("TASK_RECURRENCE_RULES", tasks.NavVisiblePageCodes);   // live defect #2
        Assert.Contains("TASK_FIELD_DEFINITIONS", tasks.NavVisiblePageCodes);
        Assert.Contains("TASK_TYPES", tasks.NavVisiblePageCodes);
        Assert.Contains("TASK_DOCUMENT_LIST", tasks.NavVisiblePageCodes);
        // The work surfaces stay out of the menu — that is what makes this module a settings module, and what the
        // rename to "Görev Tanımları" says out loud.
        Assert.DoesNotContain("TASK_CREATE", tasks.NavVisiblePageCodes);       // IsNavigationVisible: false
        Assert.DoesNotContain("TASKS", tasks.NavVisiblePageCodes);

        // positional args (AccessGovernanceManifestProvider)
        var accessGovernance = Single(manifests, "AccessGovernanceManifestProvider.cs");
        Assert.Contains("USERS", accessGovernance.NavVisiblePageCodes);
        Assert.DoesNotContain("PERMISSIONS", accessGovernance.NavVisiblePageCodes); // positional false
    }

    [Fact]
    public void The_key_transform_is_the_shipping_bridge_not_a_copy()
    {
        // If this ever needs its own implementation of Normalize, the guard has lost its point.
        Assert.Equal("TASKRECURRENCERULES", NavNameLocalizer.Normalize("TASK_RECURRENCE_RULES"));
        Assert.Equal("WORKAGGREGATION", NavNameLocalizer.Normalize("work-aggregation"));
        Assert.Equal(NavNameLocalizer.Normalize("DocumentManagement"), NavNameLocalizer.Normalize("DOCUMENT-MANAGEMENT"));
    }

    // ── manifest source reading ─────────────────────────────────────────────────────────────────────────────

    private sealed record ParsedManifest(
        string File,
        string ModuleCode,
        string Domain,
        IReadOnlyList<string> NavVisiblePageCodes,
        int ParsedPageCount,
        int RawPageOccurrences);

    private const string PageCtor = "new ModuleManifestPage(";

    // A SECOND, DUMBER MEASUREMENT of the same fact, on purpose. It reads `Domain: "X"` straight out of the
    // provider sources and never touches SplitTopLevelArguments/TryReadPage, so it cannot fail in the same way
    // the parser does. Its only job is to be the floor the parser-derived set has to clear.
    private static int DistinctDomainCountFromRawSources()
    {
        var domains = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in ProviderFiles())
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"(?<![A-Za-z])Domain:\s*""(?<value>[^""]+)"""))
            {
                domains.Add(NavNameLocalizer.Normalize(match.Groups["value"].Value));
            }
        }

        return domains.Count;
    }

    private static ParsedManifest Single(IEnumerable<ParsedManifest> manifests, string fileName) =>
        manifests.Single(m => Path.GetFileName(m.File) == fileName);

    private static IReadOnlyList<string> ProviderFiles() => Directory
        .EnumerateFiles(Path.Combine(RepoRoot(), "services"), "*ManifestProvider.cs", SearchOption.AllDirectories)
        .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        .Where(p => !Path.GetFileName(p).StartsWith("I", StringComparison.Ordinal))   // the interfaces
        .OrderBy(p => p, StringComparer.Ordinal)
        .ToList();

    private static IReadOnlyList<ParsedManifest> ReadManifests() =>
        ProviderFiles().Select(ParseProvider).ToList();

    private static ParsedManifest ParseProvider(string path)
    {
        var source = File.ReadAllText(path);
        var consts = Regex.Matches(source, @"const\s+string\s+(?<name>\w+)\s*=\s*""(?<value>[^""]*)""")
            .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value, StringComparer.Ordinal);

        var moduleCode = Resolve(FirstNamedArgument(source, "ModuleCode"), consts);
        var domain = Resolve(FirstNamedArgument(source, "Domain"), consts);

        var navVisible = new List<string>();
        var parsed = 0;
        var raw = 0;
        var index = source.IndexOf(PageCtor, StringComparison.Ordinal);
        while (index >= 0)
        {
            raw++;
            var args = SplitTopLevelArguments(source, index + PageCtor.Length);
            if (args is not null && TryReadPage(args, consts, out var pageCode, out var isNavVisible))
            {
                parsed++;
                if (isNavVisible)
                {
                    navVisible.Add(pageCode);
                }
            }

            index = source.IndexOf(PageCtor, index + PageCtor.Length, StringComparison.Ordinal);
        }

        return new ParsedManifest(path, moduleCode, domain, navVisible, parsed, raw);
    }

    // Named form:      new ModuleManifestPage(PageCode: X, ..., IsNavigationVisible: true, ...)
    // Positional form: new ModuleManifestPage("USERS", "Users", "/Users", perm, null, true, "List", 10, [])
    // The positional indexes come from the shared record ModuleManifestPage(PageCode, DisplayName, RoutePath,
    // RequiredPermission, ParentPageCode, IsNavigationVisible, PageType, SortOrder, Actions).
    private static bool TryReadPage(
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string> consts,
        out string pageCode,
        out bool isNavigationVisible)
    {
        pageCode = string.Empty;
        isNavigationVisible = false;

        var named = args
            .Select(a => Regex.Match(a, @"^(?<name>\w+)\s*:\s*(?<value>.+)$", RegexOptions.Singleline))
            .Where(m => m.Success)
            .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value.Trim(), StringComparer.Ordinal);

        if (named.TryGetValue("PageCode", out var namedCode) && named.TryGetValue("IsNavigationVisible", out var namedFlag))
        {
            pageCode = Resolve(namedCode, consts);
            isNavigationVisible = namedFlag == "true";
            return pageCode.Length > 0;
        }

        if (args.Count >= 6 && named.Count == 0)
        {
            pageCode = Resolve(args[0], consts);
            isNavigationVisible = args[5].Trim() == "true";
            return pageCode.Length > 0;
        }

        return false;
    }

    private static string FirstNamedArgument(string source, string name)
    {
        var match = Regex.Match(source, $@"(?<![A-Za-z]){name}:\s*(?<value>""[^""]*""|\w+)");
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }

    // A bare identifier is a const reference (TaskManifestProvider does this); anything else must be a literal.
    private static string Resolve(string token, IReadOnlyDictionary<string, string> consts)
    {
        token = token.Trim();
        if (token.StartsWith('"') && token.EndsWith('"') && token.Length >= 2)
        {
            return token[1..^1];
        }

        return consts.TryGetValue(token, out var value) ? value : string.Empty;
    }

    // Splits a C# argument list into top-level arguments, ignoring commas nested in (), [], <> or strings, and
    // dropping // and /* */ comments (manifest files are heavily commented between arguments).
    private static IReadOnlyList<string>? SplitTopLevelArguments(string source, int start)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var i = start;

        while (i < source.Length)
        {
            var c = source[i];

            if (c == '"')
            {
                var end = i + 1;
                while (end < source.Length && (source[end] != '"' || source[end - 1] == '\\'))
                {
                    end++;
                }

                current.Append(source, i, end - i + 1);
                i = end + 1;
                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    return null;
                }

                i = end + 2;
                continue;
            }

            if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                if (c == ')' && depth == 0)
                {
                    args.Add(current.ToString().Trim());
                    return args;
                }

                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                args.Add(current.ToString().Trim());
                current.Clear();
                i++;
                continue;
            }

            current.Append(c);
            i++;
        }

        return null; // unbalanced — treated as a parse failure, which the occurrence-count guard turns red.
    }

    private static Dictionary<string, string> ResxRows(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Where(d => d.Attribute("name") is not null)
            .ToDictionary(
                d => (string)d.Attribute("name")!,
                d => d.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repo root (frontend/Diten.Web/Resources) from the test output directory.");
    }
}

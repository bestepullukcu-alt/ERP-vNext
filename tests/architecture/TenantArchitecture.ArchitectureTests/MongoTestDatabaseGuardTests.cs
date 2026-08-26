using System.Text;
using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE GUARD — A MONGO TEST DOES NOT BUILD ITSELF A DATABASE (2026-08-26).
 *
 * WHY THIS FILE EXISTS. Measured, not guessed: a Mongo-backed test in this repo opens a database whose NAME
 * carries a fresh GUID, so every test class gets a database of its own, and several of them then call
 * `MongoDbIndexConfigurations.EnsureIndexesAsync` on it — which builds the WHOLE platform schema inside that
 * throwaway database. Platform's configuration alone touches 76 distinct collections and declares 218 index
 * models; each collection and each index is files on disk. One full run walks past macOS's 10,240
 * open-files-per-process limit and `mongod` kills itself with fassert.
 *
 * AND THE FAILURE IS SELF-FEEDING. When mongod dies mid-run, `DisposeAsync` never executes, so the throwaway
 * databases are never dropped. The next run starts on top of the wreckage of the last one.
 *
 * WHAT THIS GUARD IS NOT. It does not fix a single test — Part B does that, with the GSKU owners, and the
 * shared harnesses are not to be touched before then. This file only makes the shape impossible to ADD to,
 * and makes the size of the existing debt impossible to lose track of.
 *
 * ⚠ THIS GUARD MUST NOT NEED MONGO. It reads source text. It is green on a machine where mongod is dead —
 * which is the only state in which anyone will think to read it.
 *
 * ⚠ KNOWN WEAKNESS, STATED RATHER THAN HIDDEN. This matches SOURCE TEXT, not a parsed syntax tree. So:
 *   • renaming the variable (`var scratch = "x" + Guid.NewGuid()`) evades PER_RUN_DB;
 *   • a violation assembled across two statements evades it;
 *   • the EnsureIndexes check is a bare token match — any mention outside a call trips it.
 * Comments ARE stripped before matching (see `WithoutComments`), so prose about this rule cannot create a
 * false positive, and a violation cannot hide behind `//`. String literals are deliberately KEPT, because
 * the offending name usually lives inside one: `$"diten_brd_gsku_{Guid.NewGuid():N}"`.
 * The honest upgrade is a Roslyn pass that resolves the argument of `GetDatabase(...)` — logged as backlog,
 * not done here, because the two-token shape is what is actually spreading.
 */
public class MongoTestDatabaseGuardTests
{
    /*
     * ── VIOLATION 1: A DATABASE PER RUN ───────────────────────────────────────────────────────────────────
     * A database name assembled from `Guid.NewGuid()`. Anchored on the IDENTIFIER so that `TenantId =
     * Guid.NewGuid()` — which is the CORRECT way to isolate, and the shape the rule asks for — is not caught.
     */
    private static readonly Regex PerRunDatabaseName = new(
        @"\b(?:databaseName|dbName|DatabaseName|_databaseName|_dbName)\b[^;]{0,400}?Guid\.NewGuid",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /* The inline form, where the name is never bound to a variable at all. */
    private static readonly Regex PerRunDatabaseInline = new(
        @"GetDatabase\s*\([^;]{0,400}?Guid\.NewGuid",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /*
     * ── VIOLATION 2: THE TEST BUILDS THE PLATFORM SCHEMA ───────────────────────────────────────────────────
     * `EnsureIndexesAsync` is production bootstrap. Called from a test, against a database that exists for the
     * duration of one class, it is the file-descriptor cost multiplied by the number of test classes.
     */
    private static readonly Regex TestSideIndexBuild = new(
        @"\bEnsureIndexesAsync\b", RegexOptions.Compiled);

    /*
     * ── THE EXCEPTION LISTS ────────────────────────────────────────────────────────────────────────────────
     *
     * Every file named here had the shape BEFORE this guard existed. They are listed so the guard can be true
     * today. Each line is a debt with an owner, not a blessing.
     *
     * ⚠ THE TWO SHARED HARNESSES ARE DELIBERATELY UNTOUCHED IN THIS ROUND (owner + CONTROL TOWER,
     * 2026-08-26): `BusinessReferenceData/BusinessReferenceDataGskuCatalogLoadMongoTests.cs` (which declares
     * `BusinessReferenceDataTestHarness`, used by 7 test classes) and `Persistence/MongoIntegrationHarness.cs`
     * (used by 7 more). Changing either moves 14 classes at once. That is Part B, and it is joint work with
     * the GSKU team.
     *
     * TO REMOVE A LINE: make that test share a database and isolate by TenantId, then delete its entry.
     * Never add a line to make a red test green — that is the failure this file was written to stop.
     */
    private static readonly string[] KnownPerRunDatabase =
    {
        "services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductAbbreviationPermissionOnboardingMongoTests.cs",
        "services/Diten.HcmService/tests/Diten.HcmService.Application.Tests/EmployeeDraftSessionRepositoryMongoTests.cs",
        "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/AuditIntentDeliveryMongoTests.cs",
        "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationMongoTests.cs",
        "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductApiMongoTests.cs",
        "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRegisterMongoTests.cs",
        "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityMongoRoundTripTests.cs",
        "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuDraftFoundationMongoTests.cs",
        "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRegisterMongoTests.cs",
        "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterMongoTests.cs",
        "services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductItemSkuMasterMongoTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataGskuCatalogLoadMongoTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataPublishOperationMongoTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataTenantAssignmentMongoTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Application.Tests/Persistence/MongoIntegrationHarness.cs",
        "services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/WorkflowTransitionGateMongoRepositoryTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/RabbitMqEventingIntegrationTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/TenantLifecycleRabbitMqIntegrationTests.cs"
    };

    private static readonly string[] KnownTestSideIndexBuild =
    {
        "services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductAbbreviationPermissionOnboardingMongoTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataGskuCatalogLoadMongoTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataPublishOperationMongoTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataTenantAssignmentMongoTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/RabbitMqEventingIntegrationTests.cs",
        "services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/TenantLifecycleRabbitMqIntegrationTests.cs"
    };

    [Fact]
    public void NoTestCreatesItsOwnDatabasePerRun()
    {
        /*
         * MUTATION GUARD: write `var databaseName = "x_" + Guid.NewGuid();` in any test file not on the list
         * — a new module's Mongo test, a copy-paste of an existing one — and this goes red with that file's
         * path in the message.
         */
        var offenders = TestSources()
            .Where(f => PerRunDatabaseName.IsMatch(f.Body) || PerRunDatabaseInline.IsMatch(f.Body))
            .Select(f => f.RelativePath)
            .Where(p => !KnownPerRunDatabase.Contains(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "a Mongo test named its database with a fresh Guid — share one database and isolate by TenantId, "
            + "or add the file to KnownPerRunDatabase with a reason:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void PerRunDatabaseExceptionListStaysHonest()
    {
        // A stale exception is a hole: the file was fixed, the licence stayed, and the next violation slips in free.
        var stale = KnownPerRunDatabase
            .Where(p =>
            {
                var full = Path.Combine(RepoRoot(), p.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) return true;
                var body = WithoutComments(File.ReadAllText(full));
                return !PerRunDatabaseName.IsMatch(body) && !PerRunDatabaseInline.IsMatch(body);
            })
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        Assert.True(stale.Length == 0,
            "these files no longer name a database per run (or no longer exist) — remove them from "
            + "KnownPerRunDatabase:\n" + string.Join("\n", stale));
    }

    [Fact]
    public void NoTestBuildsThePlatformSchema()
    {
        var offenders = TestSources()
            .Where(f => TestSideIndexBuild.IsMatch(f.Body))
            .Select(f => f.RelativePath)
            .Where(p => !KnownTestSideIndexBuild.Contains(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "a test called EnsureIndexesAsync — that builds the entire platform schema (76 collections, 218 "
            + "index models) into a throwaway database. Let the shared test database carry the indexes once, "
            + "or add the file to KnownTestSideIndexBuild with a reason:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void SchemaBuildExceptionListStaysHonest()
    {
        var stale = KnownTestSideIndexBuild
            .Where(p =>
            {
                var full = Path.Combine(RepoRoot(), p.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) return true;
                return !TestSideIndexBuild.IsMatch(WithoutComments(File.ReadAllText(full)));
            })
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        Assert.True(stale.Length == 0,
            "these files no longer build the platform schema (or no longer exist) — remove them from "
            + "KnownTestSideIndexBuild:\n" + string.Join("\n", stale));
    }

    [Fact]
    public void TheScanActuallySeesTheTestTree()
    {
        /*
         * ⚠ REPORTED, NOT PINNED — but with a floor. A guard whose scan silently collapses to zero files is
         * green forever and believed. The dialog-one-implementation guard was green for a whole session while
         * seeing a fifth of its surface; this floor is the lesson from it. The number moves as tests are
         * added, and that is fine. What must not happen is the scan finding nothing.
         */
        var files = TestSources().ToArray();
        Assert.True(files.Length > 200,
            $"the test-tree scan collapsed — it found {files.Length} C# files under */tests/*. "
            + "Verify the walker before trusting any result above.");
    }

    // ── scanning ───────────────────────────────────────────────────────────────────────────────────────────

    private sealed record SourceFile(string RelativePath, string Body);

    /// <summary>Every C# file in a test project, comments already stripped.</summary>
    private static IEnumerable<SourceFile> TestSources()
    {
        var root = RepoRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("/obj/") && !p.Contains("/bin/") && !p.Contains("/node_modules/"))
            .Where(p => p.Contains("/tests/") || p.Contains(".Tests/"))
            // This guard is itself a test file that names both patterns; it must not report itself.
            .Where(p => !p.EndsWith("/MongoTestDatabaseGuardTests.cs", StringComparison.Ordinal))
            .Select(p => new SourceFile(
                Path.GetRelativePath(root, p).Replace('\\', '/'),
                WithoutComments(File.ReadAllText(p))))
            .ToArray();
    }

    /*
     * Removes `//` and comment blocks while KEEPING string literals, because the offending database name
     * normally lives inside one — `$"diten_brd_gsku_{Guid.NewGuid():N}"`. The scanner tracks string and char
     * literals only so that a `//` inside `"mongodb://localhost"` is not mistaken for a comment.
     * Approximate by design: raw string literals (`"""`) are treated as ordinary strings.
     */
    private static string WithoutComments(string source)
    {
        var output = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                if (i < source.Length) output.Append('\n');
                continue;
            }

            if (c == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i++;
                output.Append(' ');
                continue;
            }

            if (c == '@' && next == '"')
            {
                output.Append(c).Append(next);
                i += 2;
                while (i < source.Length)
                {
                    if (source[i] == '"')
                    {
                        if (i + 1 < source.Length && source[i + 1] == '"') { output.Append("\"\""); i += 2; continue; }
                        output.Append('"');
                        break;
                    }
                    output.Append(source[i]);
                    i++;
                }
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var quote = c;
                output.Append(c);
                i++;
                while (i < source.Length && source[i] != quote)
                {
                    if (source[i] == '\\' && i + 1 < source.Length) { output.Append(source[i]).Append(source[i + 1]); i += 2; continue; }
                    if (source[i] == '\n') break; // unterminated: bail rather than swallow the rest of the file
                    output.Append(source[i]);
                    i++;
                }
                if (i < source.Length) output.Append(source[i]);
                continue;
            }

            output.Append(c);
        }

        return output.ToString();
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}

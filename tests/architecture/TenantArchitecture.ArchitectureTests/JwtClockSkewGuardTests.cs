using System.Text;
using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE GUARD — ONE TOKEN, ONE VERDICT (BL-296, 2026-08-28).
 *
 * WHY THIS FILE EXISTS. Measured, not guessed: nine inbound JWT validators across five services, the gateway
 * and the web shell each wrote their own ClockSkew literal. Seven said 30 seconds, two said zero. For thirty
 * seconds after an access token expired, Platform accepted it and MDM refused it — one screen worked, the
 * next said "yetkiniz yok". The window is 30s wide and moves with the token, so it is close to
 * unreproducible on demand, and the symptom does not name the cause.
 *
 * The defect was never the VALUE. It was that the value was written nine times. So the fix is a single
 * constant (`Diten.BuildingBlocks.Security.Secrets.JwtValidationDefaults.ClockSkew`) and this guard, which
 * makes writing a tenth one impossible.
 *
 * ⚠ THIS GUARD MUST NOT NEED A RUNNING SERVICE, MONGO, OR A TOKEN. It reads source text, so it is green on
 * a machine where nothing is up — which is the only state in which anyone will think to read it.
 *
 * ⚠ KNOWN WEAKNESS, STATED RATHER THAN HIDDEN. This matches SOURCE TEXT, not a parsed syntax tree. So:
 *   • a validator assembled across statements (`var p = new TokenValidationParameters(); p.ClockSkew = …;`
 *     in another file than the one declaring ValidateLifetime) evades the pairing rule;
 *   • a service that builds its parameters in a helper class in a DIFFERENT file still passes rule 2 as long
 *     as neither file pairs `ValidateLifetime = true` with a bare literal.
 * Comments ARE stripped before matching (see `WithoutComments`), so the prose above cannot create a false
 * positive, and a violation cannot hide behind `//`. The honest upgrade is a Roslyn pass over
 * TokenValidationParameters initialisers; it is not done here, because the single-initialiser shape is the
 * one that actually exists in this repo — all nine sites, measured.
 */
public class JwtClockSkewGuardTests
{
    /// <summary>The one file allowed to name a raw TimeSpan for clock skew: the constant's own declaration.</summary>
    private const string TheSingleSource =
        "services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/JwtValidationDefaults.cs";

    private const string SharedConstant = "JwtValidationDefaults.ClockSkew";

    /*
     * ── VIOLATION 1: A VALIDATOR WRITES ITS OWN TOLERANCE ──────────────────────────────────────────────────
     * Any `ClockSkew = <anything that is not the shared constant>`. This is the shape that produced BL-296.
     */
    private static readonly Regex LocalClockSkewLiteral = new(
        // ⚠ The `\s*` belongs INSIDE the lookahead, not before it. With `=\s*(?!Jwt…)` the greedy `\s*`
        // backtracks to zero width, the lookahead is then tested against a SPACE, does not match
        // `JwtValidationDefaults`, and the negative lookahead succeeds — reporting all nine correct files as
        // violations. Measured, not guessed: that is exactly what the first version of this guard did.
        @"\bClockSkew\s*=(?!\s*JwtValidationDefaults\s*\.\s*ClockSkew\b)",
        RegexOptions.Compiled);

    /*
     * ── VIOLATION 2: A VALIDATOR DECIDES LIFETIME WITHOUT SAYING WITH WHAT TOLERANCE ───────────────────────
     * `ValidateLifetime = true` and no mention of the shared constant anywhere in the file. Deleting the
     * ClockSkew line does not make a service strict — it hands it the LIBRARY DEFAULT OF FIVE MINUTES, which
     * is ten times looser than anything in this system and completely silent. That is the trap this rule
     * exists for, and it is the rule a brand-new service trips first.
     */
    private static readonly Regex ValidatesLifetime = new(
        @"\bValidateLifetime\s*=\s*true\b", RegexOptions.Compiled);

    [Fact]
    public void NoProductionValidatorWritesItsOwnClockSkew()
    {
        /*
         * MUTATION GUARD: write `ClockSkew = TimeSpan.Zero` in any production validator — a new service, a
         * copy-paste of an existing Program.cs — and this goes red with that file's path in the message.
         */
        var offenders = ProductionSources()
            .Where(f => f.RelativePath != TheSingleSource)
            .Where(f => LocalClockSkewLiteral.IsMatch(f.Body))
            .Select(f => f.RelativePath)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "a JWT validator wrote its own ClockSkew instead of using " + SharedConstant + " — that is BL-296 "
            + "coming back: the same token judged differently by two services, 30 seconds wide and invisible "
            + "from the symptom. Use the shared constant; if the value itself is wrong, change it in "
            + TheSingleSource + " so every service moves together:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryLifetimeValidatingFileDeclaresTheSharedSkew()
    {
        var offenders = ProductionSources()
            .Where(f => ValidatesLifetime.IsMatch(f.Body))
            .Where(f => !f.Body.Contains(SharedConstant, StringComparison.Ordinal))
            .Select(f => f.RelativePath)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "this file validates token lifetime but never names " + SharedConstant + ". With no ClockSkew set, "
            + "Microsoft.IdentityModel applies its own default of FIVE MINUTES — silently looser than every "
            + "other validator in this system:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void TheSingleSourceStillDeclaresTheConstant()
    {
        // Rule 2 is satisfied by a file merely MENTIONING the constant. That is worthless if the constant
        // itself has been deleted or renamed, so pin its existence here rather than assume it.
        var full = Path.Combine(RepoRoot(), TheSingleSource.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), "the single source of clock skew is gone: " + TheSingleSource);

        var body = WithoutComments(File.ReadAllText(full));
        Assert.Matches(new Regex(@"public\s+static\s+readonly\s+TimeSpan\s+ClockSkew\s*="), body);
    }

    [Fact]
    public void TheScanActuallySeesTheProductionTree()
    {
        /*
         * ⚠ REPORTED, NOT PINNED — but with a floor. A guard whose scan silently collapses to zero files is
         * green forever and believed. Two numbers here: the file count moves as the repo grows and that is
         * fine; the validator count is the one that matters, because if the walker stops reaching
         * frontend/ or gateway/ the rules above go quiet without going red.
         */
        var files = ProductionSources().ToArray();
        Assert.True(files.Length > 500,
            $"the production scan collapsed — it found {files.Length} C# files. "
            + "Verify the walker before trusting any result above.");

        var validators = files.Count(f => ValidatesLifetime.IsMatch(f.Body));
        Assert.True(validators >= 9,
            $"the scan found only {validators} inbound JWT validators; 9 were measured on 2026-08-28 across "
            + "MDM, DevEnablement, HCM, Platform (DI + Hangfire filter), AuthService, the gateway and the web "
            + "shell (Program.cs + ShellAccessFilter). Fewer means the walker lost a tree, not that a "
            + "validator was removed — check that before lowering this number.");
    }

    // ── scanning ───────────────────────────────────────────────────────────────────────────────────────────

    private sealed record SourceFile(string RelativePath, string Body);

    /// <summary>Every production C# file (services, gateway, frontend), comments already stripped.</summary>
    private static IEnumerable<SourceFile> ProductionSources()
    {
        var root = RepoRoot();
        return new[] { "services", "gateway", "frontend" }
            .Select(d => Path.Combine(root, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("/obj/") && !p.Contains("/bin/") && !p.Contains("/node_modules/"))
            // Test code is deliberately out of scope: a test that pins exact-expiry semantics (see
            // frontend/Diten.Web.Tests/Auth/TokenBridgeTests.cs) needs ClockSkew = Zero to mean what it says,
            // and it decides nothing about a real request.
            .Where(p => !p.Contains("/tests/") && !p.Contains(".Tests/"))
            .Select(p => new SourceFile(
                Path.GetRelativePath(root, p).Replace('\\', '/'),
                WithoutComments(File.ReadAllText(p))))
            .ToArray();
    }

    /*
     * Removes `//` and comment blocks while KEEPING string literals. The scanner tracks string and char
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

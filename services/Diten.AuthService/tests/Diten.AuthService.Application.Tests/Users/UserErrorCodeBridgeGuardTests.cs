using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Application.Features.Users.Services;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ERROR-CODES-01 (BL-450) — the three ends of a Users-screen refusal, measured on the PRODUCTION files:
/// <see cref="UserErrorCodes"/> (read by reflection) ⇔ index.js <c>ERROR_CODE_KEYS</c> (parsed out of the shipped file)
/// ⇔ UsersIndex.*.resx in seven languages. Nothing here restates a code or a key: remove a code from the screen, a key
/// from one language, or the code from a refusal, and a test below goes red by name.
/// </summary>
public sealed class UserErrorCodeBridgeGuardTests
{
    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // The command handlers behind the Users screen's seven write endpoints, plus the shared refusals they call.
    private static readonly string[] ScreenCommandSources =
    [
        "Handlers/CommandHandlers/CreateUserCommandHandler.cs",
        "Handlers/CommandHandlers/UpdateUserCommandHandler.cs",
        "Handlers/CommandHandlers/DeleteUserCommandHandler.cs",
        "Handlers/CommandHandlers/SetUserActiveStatusCommandHandler.cs",
        "Handlers/CommandHandlers/ResendUserInvitationCommandHandler.cs",
        "Handlers/CommandHandlers/AdminResetPasswordCommandHandler.cs",
        "Services/UserLifecycle.cs",
        "Services/UserErrorCodes.cs",
    ];

    [Fact]
    public void Every_server_code_is_on_the_screen_and_the_screen_maps_no_code_the_server_lacks()
    {
        var server = ServerCodes().Values.ToHashSet(StringComparer.Ordinal);
        var screen = ScreenBridge().Keys.ToHashSet(StringComparer.Ordinal);

        Assert.True(server.Count > 0, "UserErrorCodes exposes no constants — the reflection read nothing.");
        var missingOnScreen = server.Except(screen).OrderBy(c => c).ToList();
        var unknownToServer = screen.Except(server).OrderBy(c => c).ToList();
        Assert.True(missingOnScreen.Count == 0,
            $"index.js ERROR_CODE_KEYS has no entry for: {string.Join(", ", missingOnScreen)} — the refusal would read as the general error.");
        Assert.True(unknownToServer.Count == 0,
            $"index.js ERROR_CODE_KEYS maps codes UserErrorCodes does not define: {string.Join(", ", unknownToServer)}");
    }

    [Fact]
    public void Every_bridged_key_is_published_and_has_its_own_text_in_all_seven_languages()
    {
        var bridge = File.ReadAllText(WebPath("Views", "Governance", "Users", "_IndexL10n.cshtml"));
        var english = ResxValues("en");
        var codes = ServerCodes().Values.ToList();

        foreach (var (code, key) in ScreenBridge())
        {
            Assert.True(bridge.Contains($"{key} = Localizer[\"{key}\"].Value", StringComparison.Ordinal),
                $"_IndexL10n.cshtml does not publish {key} ({code}) to the screen.");

            foreach (var language in SupportedLanguages)
            {
                var values = ResxValues(language);
                Assert.True(values.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text),
                    $"UsersIndex.{language}.resx is missing key: {key} ({code})");
                Assert.False(codes.Any(c => text!.Contains(c, StringComparison.Ordinal)),
                    $"UsersIndex.{language}.resx {key} shows a raw code to the reader.");
                if (language != "en")
                {
                    Assert.False(string.Equals(text, english[key], StringComparison.Ordinal),
                        $"UsersIndex.{language}.resx {key} is a copy of the English sentence.");
                }
            }
        }
    }

    [Fact]
    public void All_seven_resx_files_carry_the_same_keys()
    {
        var english = ResxValues("en").Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var language in SupportedLanguages.Where(l => l != "en"))
        {
            var keys = ResxValues(language).Keys.ToHashSet(StringComparer.Ordinal);
            Assert.True(english.SetEquals(keys),
                $"UsersIndex.{language}.resx differs from en: missing [{string.Join(", ", english.Except(keys))}] extra [{string.Join(", ", keys.Except(english))}]");
        }
    }

    [Fact]
    public void One_code_names_one_situation_and_the_six_older_codes_keep_their_values()
    {
        var codes = ServerCodes();

        var shared = codes.GroupBy(c => c.Value, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(shared.Count == 0, $"Two UserErrorCodes constants share a value: {string.Join(", ", shared)}");

        // New codes are USER_…; the one exception predates the list and has consumers.
        var misshapen = codes.Where(c => c.Key != nameof(UserErrorCodes.AccountKindPermissionDenied)
                                         && !Regex.IsMatch(c.Value, "^USER_[A-Z]+(_[A-Z]+)*$")).Select(c => c.Value).ToList();
        Assert.True(misshapen.Count == 0, $"Not in the USER_… shape: {string.Join(", ", misshapen)}");

        // Consumers pin these literals — renaming one is a breaking change, not a refactor.
        Assert.Equal("USER_EMAIL_TAKEN", UserLifecycle.EmailTakenCode);
        Assert.Equal("USER_INVITATION_PENDING", UserLifecycle.InvitationPendingCode);
        Assert.Equal("USER_QUOTA_EXCEEDED", UserLifecycle.QuotaExceededCode);
        Assert.Equal("USER_DELETE_SELF", DeleteUserCommandHandler.SelfDeleteCode);
        Assert.Equal("USER_DELETE_LAST_STEWARD", DeleteUserCommandHandler.LastStewardCode);
        Assert.Equal("USER_DEACTIVATE_SELF", SetUserActiveStatusCommandHandler.SelfDeactivateCode);
        Assert.Equal("PERM_DENIED", CreateUserCommandHandler.PermissionDeniedCode);
    }

    [Fact]
    public void No_refusal_of_a_Users_screen_command_leaves_without_a_code()
    {
        var featureRoot = Path.Combine(RepoRoot(), "services", "Diten.AuthService", "src", "Diten.AuthService.Application", "Features", "Users");
        var refusals = 0;

        foreach (var relative in ScreenCommandSources)
        {
            var source = File.ReadAllText(Path.Combine(featureRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            foreach (Match call in Regex.Matches(source, @"\.Fail\((?<args>.*?)\);", RegexOptions.Singleline))
            {
                refusals++;
                Assert.True(call.Groups["args"].Value.Contains("new ResponseError(", StringComparison.Ordinal),
                    $"{relative}: a refusal carries no stable code, so the Users screen can only say the general error → .Fail({Regex.Replace(call.Groups["args"].Value, @"\s+", " ").Trim()}");
            }
        }

        Assert.True(refusals >= 10, $"Only {refusals} refusals were found in the command sources — the scan is not reading them.");
    }

    // A rule that deliberately carries NO UserErrorCodes code — each with its reason. Anything else must carry one.
    private static readonly IReadOnlyDictionary<(string File, string Property), string> ValidatorExemptions = new Dictionary<(string, string), string>
    {
        [("CreateUserCommandValidator.cs", "Password")] =
            "self-service password ceiling: a password rule (the password.* family, out of this screen's scope); the Users form never sends a password",
        [("UpdateUserCommandValidator.cs", "Id")] =
            "the id comes from the route ({id:guid}); only Guid.Empty fails it and no screen sends that — it stays the measured example of an uncoded default (ValidationEnvelopeUnchangedTests)",
    };

    /// <summary>
    /// The create/edit validators: EVERY rule — with or without a sentence — names a <see cref="UserErrorCodes"/> code
    /// on each of its checks, or stands in <see cref="ValidatorExemptions"/> with its reason. A rule with neither
    /// would leave as FluentValidation's default code, which never reaches the wire.
    /// </summary>
    [Fact]
    public void Every_user_validator_rule_carries_a_code_or_is_an_explicit_exemption()
    {
        var validators = Path.Combine(RepoRoot(), "services", "Diten.AuthService", "src", "Diten.AuthService.Application", "Features", "Users", "Validators");
        var coded = 0;
        var exempted = new HashSet<(string, string)>();

        foreach (var file in new[] { "CreateUserCommandValidator.cs", "UpdateUserCommandValidator.cs" })
        {
            var source = File.ReadAllText(Path.Combine(validators, file));
            foreach (var rule in source.Split("RuleFor(").Skip(1))
            {
                var property = Regex.Match(rule, @"^x => x\.(?<name>[A-Za-z]+)\)").Groups["name"].Value;
                Assert.False(string.IsNullOrEmpty(property), $"{file}: could not read the property of → RuleFor({rule.Split('\n')[0].Trim()}");
                if (ValidatorExemptions.ContainsKey((file, property)))
                {
                    exempted.Add((file, property));
                    continue;
                }

                var checks = Regex.Matches(rule, @"\.(NotEmpty|NotNull|EmailAddress|MaximumLength|MinimumLength|Length|Must|Matches|InclusiveBetween|Equal|NotEqual)\(").Count;
                var codes = Regex.Matches(rule, @"\.WithErrorCode\(UserErrorCodes\.[A-Za-z]+\)").Count;
                Assert.True(checks > 0, $"{file}: {property} has no check this guard recognises — add it to the scan.");
                Assert.True(checks == codes,
                    $"{file}: {property} has {checks} check(s) but {codes} UserErrorCodes code(s), and is not an exemption.");
                coded += codes;
            }
        }

        Assert.True(coded >= 10, $"Only {coded} coded checks were found — the scan is not reading the rules.");
        var stale = ValidatorExemptions.Keys.Except(exempted).ToList();
        Assert.True(stale.Count == 0, $"Exemptions that match no rule any more: {string.Join(", ", stale)}");
    }

    // ── the production sources ──────────────────────────────────────────────────────────────────────────

    /// <summary>constant name → value, straight off <see cref="UserErrorCodes"/>.</summary>
    private static Dictionary<string, string> ServerCodes() =>
        typeof(UserErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!, StringComparer.Ordinal);

    /// <summary>code → resx key, parsed out of the shipped index.js <c>ERROR_CODE_KEYS</c> object.</summary>
    private static Dictionary<string, string> ScreenBridge()
    {
        var source = File.ReadAllText(WebPath("wwwroot", "assets", "js", "Governance", "Users", "index.js"));
        var declaration = Regex.Matches(source, @"const ERROR_CODE_KEYS = \{(?<body>[^}]*)\};");
        Assert.True(declaration.Count == 1, $"index.js declares ERROR_CODE_KEYS {declaration.Count} times; the guard expects exactly one.");

        var entries = Regex.Matches(declaration[0].Groups["body"].Value, @"(?<code>[A-Z][A-Z_]*):\s*'(?<key>[A-Za-z]+)'");
        Assert.True(entries.Count > 0, "index.js ERROR_CODE_KEYS is empty.");
        return entries.ToDictionary(m => m.Groups["code"].Value, m => m.Groups["key"].Value, StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ResxValues(string language) =>
        XDocument.Load(WebPath("Resources", "Views", "Governance", "Users", $"UsersIndex.{language}.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static string WebPath(params string[] parts) => Path.Combine([RepoRoot(), "frontend", "Diten.Web", .. parts]);

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

        throw new DirectoryNotFoundException("Could not locate the repo root (frontend/Diten.Web/Resources) from the test output directory.");
    }
}

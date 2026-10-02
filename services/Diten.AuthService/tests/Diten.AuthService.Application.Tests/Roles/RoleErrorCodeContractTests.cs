using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.AuthService.Application.Features.Roles;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Application.Features.Roles.Validators;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 (A) — the role refusal-code bridge, in the shape of <c>UserLifecycleErrorCodeContractTests</c>:
/// AuthService emits a stable code (<see cref="RoleErrorCodes"/>), each of the three screens maps the codes IT can meet
/// to one of its own resx keys (index.js <c>ERROR_CODE_KEYS</c>), the key is published to the screen
/// (<c>_IndexL10n.cshtml</c>) and exists in all seven languages. Everything is read from the production files; the
/// code column is the Auth constant, so renaming a constant on the Auth side alone turns this red.
/// </summary>
public sealed class RoleErrorCodeContractTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private sealed record Screen(string Folder, string ResxClass, IReadOnlyDictionary<string, string> CodeToKey);

    // screen ⇔ the codes its Auth endpoints can answer ⇔ the resx key of THAT screen
    private static readonly Screen[] Screens =
    [
        new("Roles", "RolesIndex", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RoleErrorCodes.ActorRequired] = "ErrorRoleActorRequired",
            [RoleErrorCodes.NotFound] = "ErrorRoleNotFound",
            [RoleErrorCodes.NameTaken] = "ErrorRoleNameTaken",
            [RoleErrorCodes.SystemNotDeletable] = "ErrorRoleSystemNotDeletable",
            [RoleErrorCodes.NameRequired] = "ErrorRoleNameRequired",
            [RoleErrorCodes.NameTooLong] = "ErrorRoleNameTooLong",
            [RoleErrorCodes.DisplayNameRequired] = "ErrorRoleDisplayNameRequired",
            [RoleErrorCodes.DisplayNameTooLong] = "ErrorRoleDisplayNameTooLong"
        }),
        new("RoleAssignments", "RoleAssignmentsIndex", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RoleErrorCodes.ActorRequired] = "ErrorRoleActorRequired",
            [RoleErrorCodes.NotFound] = "ErrorRoleNotFound",
            [RoleErrorCodes.PermissionNotTenantAssignable] = "ErrorRolePermissionNotTenantAssignable",
            [RoleErrorCodes.PermissionGrantManaged] = "ErrorRolePermissionGrantManaged"
        }),
        new("UserRoleAssignments", "UserRoleAssignmentsIndex", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RoleErrorCodes.ActorRequired] = "ErrorRoleActorRequired",
            [RoleErrorCodes.NotFound] = "ErrorRoleNotFound",
            [RoleErrorCodes.UserRoleUserNotFound] = "ErrorUserRoleUserNotFound"
        })
    ];

    private static IReadOnlySet<string> AllCodes => typeof(RoleErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Every_code_constant_is_mapped_by_at_least_one_screen_and_no_screen_expects_a_code_that_does_not_exist()
    {
        var expected = Screens.SelectMany(s => s.CodeToKey.Keys).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(AllCodes.OrderBy(x => x), expected.OrderBy(x => x));
        Assert.All(AllCodes, code => Assert.Matches("^(ROLE|ROLE_PERMISSION|USER_ROLE)_[A-Z_]+$", code));
    }

    [Fact]
    public void Each_screen_maps_exactly_its_codes_to_its_keys()
    {
        foreach (var screen in Screens)
        {
            var js = File.ReadAllText(Web("wwwroot", "assets", "js", "Governance", screen.Folder, "index.js"));
            var map = Regex.Match(js, @"const ERROR_CODE_KEYS = \{(?<body>[^}]*)\}");
            Assert.True(map.Success, $"{screen.Folder}/index.js has no ERROR_CODE_KEYS.");
            var declared = Regex.Matches(map.Groups["body"].Value, @"(?<code>[A-Z_]+): '(?<key>[A-Za-z]+)'")
                .ToDictionary(m => m.Groups["code"].Value, m => m.Groups["key"].Value, StringComparer.Ordinal);

            Assert.True(screen.CodeToKey.OrderBy(x => x.Key).SequenceEqual(declared.OrderBy(x => x.Key)),
                $"{screen.Folder}/index.js ERROR_CODE_KEYS [{string.Join(", ", declared.Select(x => x.Key + "→" + x.Value))}] "
                + $"differs from the contract [{string.Join(", ", screen.CodeToKey.Select(x => x.Key + "→" + x.Value))}]");
            // The screen says the refusal through the shared helper — never the server's sentence.
            Assert.Contains("window.DitenRefusal.message(json, ERROR_CODE_KEYS", js, StringComparison.Ordinal);
            Assert.DoesNotContain("(json.errors || [])[0]", js, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_key_is_published_to_its_screen_and_exists_in_all_seven_languages_with_its_own_text()
    {
        foreach (var screen in Screens)
        {
            var bridge = File.ReadAllText(Web("Views", "Governance", screen.Folder, "_IndexL10n.cshtml"));
            var index = File.ReadAllText(Web("Views", "Governance", screen.Folder, "Index.cshtml"));
            Assert.Contains("~/assets/js/shared/diten-refusal.js", index, StringComparison.Ordinal);

            var english = ResxValues(screen, "en");
            foreach (var key in screen.CodeToKey.Values)
            {
                Assert.True(bridge.Contains($"{key} = Localizer[\"{key}\"].Value", StringComparison.Ordinal),
                    $"{screen.Folder}/_IndexL10n.cshtml does not publish {key} to the screen.");

                foreach (var language in Languages)
                {
                    var values = ResxValues(screen, language);
                    Assert.True(values.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text),
                        $"{screen.ResxClass}.{language}.resx is missing key: {key}");
                    // One sentence for the reader: no code, no id, no technical term.
                    Assert.DoesNotMatch("ROLE_|USER_ROLE_|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}|errorCode|HTTP|\\b40[0-9]\\b", text!);
                    if (language != "en")
                    {
                        Assert.True(text != english[key], $"{screen.ResxClass}.{language}.resx {key} is a copy of the English sentence.");
                    }
                }
            }
        }
    }

    [Fact]
    public void The_seven_resx_files_of_each_screen_carry_the_same_keys()
    {
        foreach (var screen in Screens)
        {
            var english = ResxValues(screen, "en").Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            foreach (var language in Languages)
            {
                Assert.Equal(english, ResxValues(screen, language).Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
            }
        }
    }

    // The validator codes cannot be asserted over HTTP on this branch (their passage through ExceptionHandlingBehavior
    // belongs to another work package), so they are proven where they are produced.
    [Theory]
    [InlineData("", "Reviewers", RoleErrorCodes.NameRequired)]
    [InlineData("123456789012345678901234567890123456789012345678901", "Reviewers", RoleErrorCodes.NameTooLong)]
    [InlineData("reviewers", "", RoleErrorCodes.DisplayNameRequired)]
    public void The_create_validator_tags_each_failure_with_its_code(string name, string displayName, string code)
    {
        var result = new CreateRoleCommandValidator().Validate(new CreateRoleCommand(name, displayName, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == code);
    }

    [Fact]
    public void A_display_name_over_100_characters_is_tagged_ROLE_DISPLAY_NAME_TOO_LONG()
    {
        var result = new CreateRoleCommandValidator().Validate(new CreateRoleCommand("reviewers", new string('x', 101), null));

        Assert.Contains(result.Errors, e => e.ErrorCode == RoleErrorCodes.DisplayNameTooLong);
    }

    private static Dictionary<string, string> ResxValues(Screen screen, string language)
        => XDocument.Load(Web("Resources", "Views", "Governance", screen.Folder, $"{screen.ResxClass}.{language}.resx")).Root!
            .Elements("data")
            .Where(d => d.Attribute("name") is not null)
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static string Web(params string[] parts) => Path.Combine([RepoRoot(), "frontend", "Diten.Web", .. parts]);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repo root (frontend/Diten.Web/Resources) from the test output directory.");
    }
}

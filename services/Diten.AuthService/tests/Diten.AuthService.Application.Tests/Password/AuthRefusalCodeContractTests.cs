using System.Reflection;
using System.Xml.Linq;
using Diten.AuthService.Application.Common;

namespace Diten.AuthService.Application.Tests.Password;

/// <summary>
/// BL-529 FIX3 — every sign-in / password refusal code (<see cref="AuthRefusalCodes"/>) reaches the reader in their language:
/// code ⇔ the Web AuthGateway map ⇔ a SharedResource sentence in all seven languages. A code added without its key, or a
/// language without its sentence, is red here — the screen would otherwise show the service's English.
/// </summary>
public sealed class AuthRefusalCodeContractTests
{
    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly IReadOnlyDictionary<string, string> ExpectedCodeToResourceKey = new Dictionary<string, string>
    {
        [AuthRefusalCodes.TooManyRequests] = "Auth.Error.TooManyRequests",
        [AuthRefusalCodes.PasswordChangedMeanwhile] = "Auth.Error.PasswordChangedMeanwhile",
        [AuthRefusalCodes.AccountDeactivated] = "Auth.Error.AccountDeactivated",
        // WP-USERS-ROLES-CLOSE-FIX1 item 2 (BL-496)
        [AuthRefusalCodes.InvalidCredentials] = "Auth.Error.InvalidCredentials",
        [AuthRefusalCodes.LoginUnavailable] = "Auth.Error.LoginUnavailable",
        [AuthRefusalCodes.EmailLoginDisabled] = "Auth.Error.EmailLoginDisabled",
        [AuthRefusalCodes.TemporaryPasswordExpired] = "Auth.Error.TemporaryPasswordExpired",
        [AuthRefusalCodes.PlatformPrivilegesRequired] = "Auth.Error.PlatformPrivilegesRequired",
        [AuthRefusalCodes.InvalidVerificationCode] = "Auth.Error.InvalidVerificationCode",
        [AuthRefusalCodes.CurrentPasswordIncorrect] = "Auth.Error.CurrentPasswordIncorrect",
        [AuthRefusalCodes.SessionEnded] = "Auth.Error.SessionEnded",
        [AuthRefusalCodes.LinkInvalid] = "Auth.Error.LinkInvalid",
    };

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 item 2 — the Web doors' OWN sentences (said when a refusal arrives without a code the
    /// bridge knows, or before the service is asked): also in all seven languages.</summary>
    private static readonly string[] WebDoorKeys =
    [
        "Auth.Error.LoginFailed", "Auth.Error.VerificationFailed", "Auth.Error.PasswordChangeFailed", "Auth.Error.ServiceUnavailable",
        "Auth.Error.RequestInvalid", "Auth.Error.TenantMissing", "Auth.Error.VerificationCodeRequired",
        "Auth.Error.VerificationChallengeRequired", "Auth.Dialog.Ok", "Auth.Mfa.Title", "Auth.Mfa.Prompt", "Auth.Mfa.CodeLabel",
        "Auth.Mfa.Resend", "Auth.Mfa.Verifying", "Auth.Mfa.Resending", "Auth.Mfa.ResentTitle", "Auth.Mfa.ResentMessage"
    ];

    [Fact]
    public void Every_refusal_code_constant_has_exactly_one_resource_key()
    {
        var declared = typeof(AuthRefusalCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal);

        Assert.Equal(declared, ExpectedCodeToResourceKey.Keys.OrderBy(c => c, StringComparer.Ordinal));
    }

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX2 item 6 — a door's own code constant is one of the shared codes (the platform forced
    /// change's expired temporary password used to send "PLATFORM_TEMPORARY_PASSWORD_EXPIRED", a code the Web had no
    /// sentence for).</summary>
    [Fact]
    public void The_platform_forced_change_sends_the_shared_expired_temporary_password_code()
        => Assert.Equal(AuthRefusalCodes.TemporaryPasswordExpired, Diten.AuthService.Api.Controllers.PlatformAuthController.TemporaryPasswordExpiredCode);

    [Fact]
    public void The_web_gateway_maps_every_code_to_its_key()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Services", "Auth", "AuthGateway.cs"));
        foreach (var (code, key) in ExpectedCodeToResourceKey)
        {
            Assert.Contains($"[\"{code}\"] = \"{key}\"", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_key_has_a_sentence_in_all_seven_languages()
    {
        var resources = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources");
        foreach (var language in SupportedLanguages)
        {
            var values = XDocument.Load(Path.Combine(resources, $"SharedResource.{language}.resx")).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
            foreach (var key in ExpectedCodeToResourceKey.Values.Concat(WebDoorKeys))
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"SharedResource.{language}.resx has no sentence for {key}");
            }
        }

        // And each language says it in its own words (not the English copied over).
        foreach (var key in ExpectedCodeToResourceKey.Values.Concat(WebDoorKeys))
        {
            var sentences = SupportedLanguages.Select(language => XDocument.Load(Path.Combine(resources, $"SharedResource.{language}.resx"))
                .Root!.Elements("data").Single(d => (string)d.Attribute("name")! == key).Element("value")!.Value);
            Assert.Equal(SupportedLanguages.Length, sentences.Distinct().Count());
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
}

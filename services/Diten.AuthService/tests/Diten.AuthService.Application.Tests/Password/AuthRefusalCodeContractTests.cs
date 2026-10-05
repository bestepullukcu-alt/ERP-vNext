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
    };

    [Fact]
    public void Every_refusal_code_constant_has_exactly_one_resource_key()
    {
        var declared = typeof(AuthRefusalCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal);

        Assert.Equal(declared, ExpectedCodeToResourceKey.Keys.OrderBy(c => c, StringComparer.Ordinal));
    }

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
            foreach (var key in ExpectedCodeToResourceKey.Values)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"SharedResource.{language}.resx has no sentence for {key}");
            }
        }

        // And each language says it in its own words (not the English copied over).
        foreach (var key in ExpectedCodeToResourceKey.Values)
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

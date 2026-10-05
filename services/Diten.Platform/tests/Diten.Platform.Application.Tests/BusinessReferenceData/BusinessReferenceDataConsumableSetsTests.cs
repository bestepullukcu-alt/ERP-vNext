using System.Text.Json;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS — the consumable-sets allow-list has two homes that must say the same thing: the shipped
/// configuration (<c>BusinessReferenceData:ConsumableSets</c> in the Platform API appsettings.json) and the code default
/// that applies when that key is missing. Both are PRODUCTION artefacts; neither is copied here. The CRM side of the
/// contract (every set code CRM consumes is listed) is <c>CrmReferenceSetDriftGuardTests</c> in the CRM test project.
/// </summary>
public sealed class BusinessReferenceDataConsumableSetsTests
{
    private static string ApiDirectory() =>
        Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.API");

    private static IReadOnlyList<string> ConfiguredList()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(ApiDirectory(), "appsettings.json")));
        return document.RootElement
            .GetProperty("BusinessReferenceData")
            .GetProperty("ConsumableSets")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToList();
    }

    [Fact]
    public void The_shipped_configuration_and_the_code_default_list_the_same_sets()
    {
        var configured = ConfiguredList();
        var defaults = BusinessReferenceDataConsumableSetsOptions.DefaultConsumableSets;

        Assert.Equal(configured.Count, configured.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(defaults.Count, defaults.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(defaults.OrderBy(x => x, StringComparer.Ordinal), configured.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void No_environment_file_overrides_the_list()
    {
        // .NET configuration merges arrays BY INDEX: a shorter list in appsettings.Development.json would silently keep
        // the tail of the base list and replace its head. The list therefore lives in appsettings.json only.
        var overriding = Directory.GetFiles(ApiDirectory(), "appsettings.*.json")
            .Where(path => File.ReadAllText(path).Contains("\"ConsumableSets\"", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(overriding);
    }

    [Fact]
    public void The_five_mobile_sets_and_the_global_country_axis_are_listed()
    {
        foreach (var code in new[]
                 {
                     "account-type", "account-status", "account-category", "contact-type", "contact-status", "COUNTRY_CODES"
                 })
        {
            Assert.Contains(code, BusinessReferenceDataConsumableSetsOptions.DefaultConsumableSets);
        }
    }

    [Theory]
    [InlineData("account-type", "account-type")]
    [InlineData("ACCOUNT-TYPE", "account-type")]
    [InlineData("  contact-status ", "contact-status")]
    [InlineData("country_codes", "COUNTRY_CODES")]
    public void A_requested_code_resolves_case_insensitively_to_its_listed_spelling(string requested, string expected)
    {
        var options = new BusinessReferenceDataConsumableSetsOptions();

        Assert.True(options.TryResolve(requested, out var resolved));
        Assert.Equal(expected, resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("qms-document-class")]
    [InlineData("legal-form")]
    public void An_unlisted_or_blank_code_does_not_resolve(string? requested)
    {
        Assert.False(new BusinessReferenceDataConsumableSetsOptions().TryResolve(requested, out _));
    }

    [Fact]
    public void An_empty_or_blank_configuration_falls_back_to_the_code_default()
    {
        var empty = new BusinessReferenceDataConsumableSetsOptions { ConsumableSets = [] };
        var blank = new BusinessReferenceDataConsumableSetsOptions { ConsumableSets = ["", "  "] };

        Assert.Equal(BusinessReferenceDataConsumableSetsOptions.DefaultConsumableSets, empty.EffectiveConsumableSets());
        Assert.Equal(BusinessReferenceDataConsumableSetsOptions.DefaultConsumableSets, blank.EffectiveConsumableSets());
    }
}

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.ContactAvailability;
using Diten.CrmService.Application.Features.ImportExport;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy;
using Diten.CrmService.Infrastructure.ReferenceValidation;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS — DRIFT GUARD: every reference set code CRM consumes is on the Platform consumable-sets list.
///
/// <para><b>Why.</b> CRM reads its reference sets with the caller's token on the Platform consumable-sets route, which any
/// tenant role may read but which answers only the sets listed in Platform configuration
/// (<c>BusinessReferenceData:ConsumableSets</c>). A CRM set that is not listed still works for an administrator (CRM falls
/// back to the Platform consumer path) but silently stops working for every other role — the bug this package fixed. So a
/// new CRM set must be listed in the same change, and this test is what notices when it is not.</para>
///
/// <para><b>Measured against production code, not a copy.</b> CRM's set codes are read by reflection from the shipped CRM
/// assemblies; the Platform list is read from the shipped Platform appsettings.json (whose equality with the Platform code
/// default is pinned by <c>BusinessReferenceDataConsumableSetsTests</c> on the Platform side). There is no list here.</para>
///
/// <para><b>What counts as a CRM set code.</b> (1) every <c>const string</c> whose name ends in <c>Set</c>; (2) every
/// <c>const string</c> declared in a <c>*ReferenceSets</c> class; (3) the set lists CRM iterates at runtime (territory and
/// visit-frequency readiness descriptors, contact-availability sets, contact workbook sets, the segment attribute catalog's
/// <c>reference-set</c> value sources); (4) a string literal passed straight to a reference reader. A constant that matches
/// (1)/(2) but is NOT a set code is named in <see cref="NotSetCodes"/> with the reason — a reviewed exception, checked to
/// still exist so the list cannot go stale.</para>
/// </summary>
public sealed class CrmReferenceSetDriftGuardTests
{
    /// <summary>Constants that look like set codes by the rules above but are not reference set codes.</summary>
    private static readonly Dictionary<string, string> NotSetCodes = new(StringComparer.Ordinal)
    {
        ["ClaimUsageItemTypes.ContentSet"] = "usage item type tag, not a reference set",
        ["ContentCompositionAuditEntities.ContentSet"] = "audit entity type tag, not a reference set",
        ["SegmentAttributeValueSource.KindReferenceSet"] = "value-source kind name, not a reference set",
        ["ClaimReferenceSets.LanguagesAttribute"] = "attribute key on country-content-languages values",
        ["ClaimReferenceSets.VerbatimAdaptation"] = "value code of claim-adaptation-type",
        ["ClaimReferenceSets.DefaultCoreLanguage"] = "language code",
        ["TerritoryReferenceSets.RankMetadataKey"] = "attribute key on territory-level values",
        ["TerritoryReferenceSets.SortOrderMetadataKey"] = "attribute key on territory-level values",
        ["TerritoryReferenceSets.DraftStatus"] = "value code of territory-model-status",
        ["TerritoryReferenceSets.BusinessUnitScopeType"] = "value code of business-scope-type"
    };

    private static readonly Assembly[] CrmAssemblies =
    [
        typeof(AccountReferenceValidation).Assembly,          // Diten.CrmService.Application
        typeof(GatewayReferenceDataValidator).Assembly,       // Diten.CrmService.Infrastructure
        typeof(Diten.CrmService.Api.Controllers.CustomBaseController).Assembly // Diten.CrmService.Api
    ];

    private static readonly Regex LiteralFirstArgument = new(
        @"\b(?:ValidateAsync|GetValueAttributesAsync|GetPublishedValuesAsync|ValidateValueAsync|GetValueMetadataAsync|ReferenceSet)\(\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex LiteralSecondArgument = new(
        @"\bValidateReferenceAsync\(\s*[_A-Za-z][\w\.]*\s*,\s*""([^""]+)""",
        RegexOptions.Compiled);

    [Fact]
    public void Every_reference_set_code_CRM_consumes_is_on_the_platform_consumable_list()
    {
        var crm = CrmSetCodes();
        var platform = PlatformConsumableSets().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = crm
            .Where(entry => !platform.Contains(entry.Code))
            .Select(entry => $"{entry.Code} (from {entry.Source})")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "CRM consumes reference sets the Platform does not list as consumable — a non-admin user cannot read them. "
            + "Add them to BusinessReferenceData:ConsumableSets in services/Diten.Platform/src/Diten.Platform.API/appsettings.json "
            + "AND to BusinessReferenceDataConsumableSetsOptions.DefaultConsumableSets: " + string.Join(", ", missing));
    }

    [Fact]
    public void The_scan_finds_the_known_crm_sets_so_it_is_not_vacuous()
    {
        var codes = CrmSetCodes().Select(entry => entry.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var expected in new[]
                 {
                     "account-type", "account-status", "account-category", "contact-type", "contact-status", "contact-role",
                     "account-relationship-type", "COUNTRY_CODES", "business-unit", "country-content-languages",
                     "claim-country-closure-reason", "claim-adaptation-type", "contact-availability-type", "territory-level",
                     "preferred-language", "phone-country-code", "visit-frequency-type"
                 })
        {
            Assert.Contains(expected, codes);
        }

        Assert.True(codes.Count >= 35, $"only {codes.Count} CRM set codes were found — discovery is broken");
        Assert.DoesNotContain("content-set", codes);
        Assert.DoesNotContain("rank", codes);
    }

    [Fact]
    public void Every_reviewed_exception_still_names_an_existing_constant()
    {
        var constants = Constants().Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        var stale = NotSetCodes.Keys.Where(name => !constants.Contains(name)).ToList();

        Assert.True(stale.Count == 0, "Stale NotSetCodes entries (constant no longer exists): " + string.Join(", ", stale));
    }

    // ---- CRM side ---------------------------------------------------------------------------------------------------

    private static IReadOnlyList<(string Code, string Source)> CrmSetCodes()
    {
        var codes = new List<(string Code, string Source)>();

        foreach (var (name, typeName, fieldName, value) in Constants())
        {
            if (NotSetCodes.ContainsKey(name))
            {
                continue;
            }

            if (fieldName.EndsWith("Set", StringComparison.Ordinal)
                || typeName.EndsWith("ReferenceSets", StringComparison.Ordinal))
            {
                codes.Add((value, name));
            }
        }

        codes.AddRange(TerritoryReferenceSets.Required.Select(d => (d.SetCode, "TerritoryReferenceSets.Required")));
        codes.AddRange(VisitFrequencyPolicyReferenceSets.Optional.Select(d => (d.SetCode, "VisitFrequencyPolicyReferenceSets.Optional")));
        codes.AddRange(ContactAvailabilityReferenceSets.All.Select(code => (code, "ContactAvailabilityReferenceSets.All")));
        codes.AddRange(ContactWorkbookSchema.AllSets.Select(code => (code, "ContactWorkbookSchema.AllSets")));

        foreach (var attribute in SegmentAttributeCatalog.All)
        {
            var sources = new[] { attribute.ValueSource }
                .Concat(attribute.ParameterValueSources?.Values ?? Enumerable.Empty<SegmentAttributeValueSource>());
            foreach (var source in sources)
            {
                if (source.Kind == SegmentAttributeValueSource.KindReferenceSet && !string.IsNullOrWhiteSpace(source.ReferenceSetCode))
                {
                    codes.Add((source.ReferenceSetCode!, $"SegmentAttributeCatalog.{attribute.AttributeCode}"));
                }
            }
        }

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(RepoRoot(), "services", "Diten.CrmService", "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (var match in LiteralFirstArgument.Matches(text).Concat(LiteralSecondArgument.Matches(text)))
            {
                codes.Add((match.Groups[1].Value, $"literal in {Path.GetFileName(file)}"));
            }
        }

        return codes;
    }

    private static IEnumerable<(string Name, string TypeName, string FieldName, string Value)> Constants()
    {
        foreach (var assembly in CrmAssemblies)
        {
            foreach (var type in assembly.GetTypes().Where(t => !t.Name.Contains('<')))
            {
                foreach (var field in type.GetFields(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (field.IsLiteral && field.FieldType == typeof(string) && field.GetRawConstantValue() is string value)
                    {
                        yield return ($"{type.Name}.{field.Name}", type.Name, field.Name, value);
                    }
                }
            }
        }
    }

    // ---- Platform side ----------------------------------------------------------------------------------------------

    private static IReadOnlyList<string> PlatformConsumableSets()
    {
        var path = Path.Combine(RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.API", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("BusinessReferenceData")
            .GetProperty("ConsumableSets")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToList();
    }

    /// <summary>The repository root, found by walking up to the AGENTS.md marker (works from a git worktree too).</summary>
    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Repo root not found above '{AppContext.BaseDirectory}' — no AGENTS.md on any parent.");
    }
}

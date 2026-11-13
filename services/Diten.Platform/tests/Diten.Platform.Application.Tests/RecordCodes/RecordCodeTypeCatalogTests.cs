using System.Text.RegularExpressions;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Domain.Enums.RecordCodes;
using Diten.Platform.Domain.Features.RecordCodes;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 AC-9 / AC-14 / AC-16 / AC-40 — the code catalogue pinned to RCS-001 §3 (read from the rule file), the
/// allowed scope per type, the consumers' closed type lists, and the source-service names the door accepts.
/// </summary>
public sealed class RecordCodeTypeCatalogTests
{
    /// <summary>The one named difference from §3 (K-7): MOD-0217 is AGR, §3 still says CTR until DL-7 (A-3).</summary>
    private static readonly (string Rule, string Catalog) NamedException = ("CTR", "AGR");

    /// <summary>The second named difference (SYNC S1, CT decision 2026-11-13, K-1) — the types §3 lists as automatic that
    /// the catalogue DELIBERATELY does not hold; a closed set, only <c>ORD</c>. RCS-001 §3 ORD (MOD-0168, kullanıcı onayı
    /// 2026-10-20): ortak üretici gelene kadar CRM kendi sayacıyla üretir; MOD-0040-FU02 §4.4 / AC-16 listesinde yok —
    /// FU-0040-FU02-ORD (paket revizyonu + CRM geçişi) ile kalkar.</summary>
    private static readonly string[] InTheRuleButNotInTheCatalogue = ["ORD"];

    [Fact]
    public void AC16_the_catalogue_holds_every_automatic_row_of_RCS001_section_3_and_PLN_and_nothing_else()
    {
        var automatic = AutomaticTypesInTheRule();

        Assert.Contains(NamedException.Rule, automatic); // §3 changed: remove the named exception from this test (DL-7)
        Assert.DoesNotContain(NamedException.Catalog, automatic);
        Assert.All(InTheRuleButNotInTheCatalogue, type =>
        {
            Assert.Contains(type, automatic); // the §3 row is gone: remove the type from the named set too
            Assert.False(RecordCodeTypeCatalog.TryGet(type, out _), $"{type} joined the catalogue: remove it from the named set (FU-0040-FU02-ORD).");
        });
        automatic = automatic.Except(InTheRuleButNotInTheCatalogue, StringComparer.Ordinal).ToArray();
        var expected = automatic.Select(type => type == NamedException.Rule ? NamedException.Catalog : type).Append("PLN")
            .OrderBy(type => type, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, RecordCodeTypeCatalog.All.Select(d => d.RecordType).OrderBy(type => type, StringComparer.Ordinal).ToArray());
        Assert.All(RecordCodeTypeCatalog.All.Where(d => d.RecordType != "PLN"), d => Assert.Equal("OTOMATİK", d.Class));
        Assert.Equal("İKİSİ DE", Assert.Single(RecordCodeTypeCatalog.All, d => d.RecordType == "PLN").Class);
    }

    [Theory]
    [InlineData("ACC")]
    [InlineData("CMP")]
    [InlineData("GP")]
    [InlineData("UID")]
    [InlineData("IT")]
    [InlineData("TTY")]
    [InlineData("PUT")]
    [InlineData("CTR")]
    [InlineData("ORD")]
    [InlineData("XYZ")]
    public void AC14_kalsin_ikisi_de_old_and_unknown_types_are_not_in_the_catalogue(string recordType) =>
        Assert.False(RecordCodeTypeCatalog.TryGet(recordType, out _));

    [Fact]
    public void AC09_each_type_allows_exactly_its_scopes_and_ORG_POS_have_no_caller_and_no_scope()
    {
        var scopes = RecordCodeTypeCatalog.All.ToDictionary(d => d.RecordType, d => d.AllowedScopes.OrderBy(s => s).ToArray());
        Assert.Equal([RecordCodePrefixScope.Group], scopes["BNK"]);
        Assert.Equal([RecordCodePrefixScope.LegalEntity, RecordCodePrefixScope.Group], scopes["BAC"]);
        foreach (var type in new[] { "PAY", "APD", "ARI", "CPS", "CTP", "AGR" })
        {
            Assert.Equal([RecordCodePrefixScope.LegalEntity], scopes[type]);
        }

        Assert.Equal([RecordCodePrefixScope.Platform], scopes["PLN"]);
        foreach (var type in new[] { "ORG", "POS", "PLN" })
        {
            var definition = Assert.Single(RecordCodeTypeCatalog.All, d => d.RecordType == type);
            Assert.Null(definition.AllowedCaller);
            Assert.Null(definition.RequiredPermission);
        }

        Assert.Empty(scopes["ORG"]);
        Assert.Empty(scopes["POS"]);
    }

    [Fact]
    public void AC14_the_consumer_map_is_the_catalogue_s_callers_and_no_binding_can_ask_for_another_s_types()
    {
        foreach (var consumer in TrustedRecordCodeCredentialAuthenticator.Consumers)
        {
            foreach (var type in consumer.RecordTypes)
            {
                Assert.True(RecordCodeTypeCatalog.TryGet(type, out var definition), type);
                Assert.Equal(consumer.SourceService, definition.AllowedCaller);
            }
        }

        var mapped = TrustedRecordCodeCredentialAuthenticator.Consumers.SelectMany(c => c.RecordTypes).ToArray();
        Assert.Equal(mapped.Length, mapped.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(RecordCodeTypeCatalog.All.Where(d => d.AllowedCaller is not null).Select(d => d.RecordType).OrderBy(t => t),
            mapped.OrderBy(t => t));
        Assert.DoesNotContain("ORG", mapped);
        Assert.DoesNotContain("POS", mapped);
        Assert.DoesNotContain("PLN", mapped);
    }

    [Fact]
    public void AC40_every_mapped_service_name_is_in_the_door_s_closed_set_with_the_Platform_itself()
    {
        Assert.Contains("Diten.Platform", TransactionOwnedAuditSourceServices.Names);
        Assert.All(TrustedRecordCodeCredentialAuthenticator.Consumers,
            consumer => Assert.Contains(consumer.SourceService, TransactionOwnedAuditSourceServices.Names));
        Assert.Equal(TrustedRecordCodeCredentialAuthenticator.Consumers.Count + 1, TransactionOwnedAuditSourceServices.Names.Count);
    }

    private static string[] AutomaticTypesInTheRule()
    {
        var rule = File.ReadAllLines(Path.Combine(RepoPaths.Root(), ".antigravity", "rules", "record-coding-standard.md"));
        var start = Array.FindIndex(rule, line => line.StartsWith("## 3.", StringComparison.Ordinal));
        var end = Array.FindIndex(rule, start + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        return rule[start..end]
            .Where(line => line.StartsWith('|'))
            .Select(line => line.Split('|').Select(cell => cell.Trim()).ToArray())
            .Where(cells => cells.Length > 4 && cells[2] == "OTOMATİK")
            .Select(cells => Regex.Match(cells[3], "`([A-Z]+)`").Groups[1].Value)
            .ToArray();
    }
}

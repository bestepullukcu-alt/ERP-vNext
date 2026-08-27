using Diten.Web.ViewComponents;
using Xunit;

namespace Diten.Web.Tests.Navigation;

// FIX-DOMAIN-NORMALIZATION — the sidebar must group modules into DOMAIN sections by the NORMALIZED domain key.
// The live bug: the catalog stored "MASTER-DATA-MANAGEMENT" (Reference Data, Legal Entity) and
// "MASTERDATAMANAGEMENT" (Product/Item/SKU). Both localize to the SAME heading, so grouping on the raw code
// printed "Ana Veri Yönetimi" TWICE with the three modules split across the two sections.
public sealed class DynamicModuleMenuDomainGroupingTests
{
    private static DynamicModuleMenuViewComponent.NavDomainEntry Entry(string domainCode, string domainDisplay, int sort, string module) =>
        new(domainCode, domainDisplay, sort,
            new NavModuleEntryView(module, [new NavNodeView(module, $"/{module}", null, null, [])]));

    [Fact]
    public void Two_spellings_of_one_domain_render_a_single_heading_with_all_modules()
    {
        var groups = DynamicModuleMenuViewComponent.GroupByDomain(
        [
            Entry("MASTER-DATA-MANAGEMENT", "Ana Veri Yönetimi", 0, "Reference Data"),
            Entry("MASTER-DATA-MANAGEMENT", "Ana Veri Yönetimi", 0, "Legal Entity"),
            Entry("MASTERDATAMANAGEMENT", "Ana Veri Yönetimi", 0, "Product/Item/SKU")
        ]);

        var group = Assert.Single(groups);
        Assert.Equal("Ana Veri Yönetimi", group.DomainDisplayName);
        Assert.Equal(
            new[] { "Reference Data", "Legal Entity", "Product/Item/SKU" },
            group.Modules.Select(m => m.ModuleDisplayName));
    }

    [Theory]
    [InlineData("PLATFORM-SHARED-SERVICES", "PlatformSharedServices")]
    [InlineData("DEVENABLEMENT", "DevEnablement")]
    [InlineData("DOCUMENT-MANAGEMENT", "Document Management")]
    public void Every_separator_and_case_variant_collapses_into_one_group(string a, string b)
    {
        var groups = DynamicModuleMenuViewComponent.GroupByDomain(
            [Entry(a, "Domain", 0, "A"), Entry(b, "Domain", 0, "B")]);

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Modules.Count);
    }

    [Fact]
    public void Genuinely_different_domains_stay_separate_and_keep_sort_order()
    {
        var groups = DynamicModuleMenuViewComponent.GroupByDomain(
        [
            Entry("WORKSPACE", "Çalışma Alanı", 2, "Tasks"),
            Entry("MASTERDATAMANAGEMENT", "Ana Veri Yönetimi", 1, "Reference Data"),
            Entry("ADMINISTRATION", "Yönetim", 0, "Access Governance")
        ]);

        Assert.Equal(
            new[] { "Yönetim", "Ana Veri Yönetimi", "Çalışma Alanı" },
            groups.Select(g => g.DomainDisplayName));
    }

    // A drifted pair whose two rows disagree on the label (l10n key missing for one) must still be ONE group with a
    // DETERMINISTIC heading — lowest sort wins — never a heading that flips between renders.
    [Fact]
    public void Display_name_within_a_group_is_picked_deterministically()
    {
        var forward = DynamicModuleMenuViewComponent.GroupByDomain(
            [Entry("MASTERDATAMANAGEMENT", "Ana Veri Yönetimi", 1, "A"), Entry("MASTER-DATA-MANAGEMENT", "MASTER-DATA-MANAGEMENT", 5, "B")]);
        var reversed = DynamicModuleMenuViewComponent.GroupByDomain(
            [Entry("MASTER-DATA-MANAGEMENT", "MASTER-DATA-MANAGEMENT", 5, "B"), Entry("MASTERDATAMANAGEMENT", "Ana Veri Yönetimi", 1, "A")]);

        Assert.Equal("Ana Veri Yönetimi", Assert.Single(forward).DomainDisplayName);
        Assert.Equal("Ana Veri Yönetimi", Assert.Single(reversed).DomainDisplayName);
    }

    [Fact]
    public void Blank_domain_codes_share_one_group_without_throwing()
    {
        var groups = DynamicModuleMenuViewComponent.GroupByDomain(
            [Entry("", "Modules", 0, "A"), Entry("   ", "Modules", 0, "B")]);

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Modules.Count);
    }
}

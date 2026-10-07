using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-3C (K-7, S-4) — the Web's mandatory compatibility: the Targets "save" still sends each doctor WITHOUT a
/// <c>products</c> field, so the session update keeps the doctor's stored product pick (null = keep, D9). The product
/// picker itself is Faz 4.
/// </summary>
public sealed class VisitPlanningProductPickWebTests
{
    [Fact]
    public void Saving_the_targets_sends_no_products_so_the_stored_pick_is_kept()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning", "details.js"));
        var save = Regex.Match(js, @"const saveTargets = \(\) => \{.*?\n    \};", RegexOptions.Singleline).Value;

        Assert.NotEmpty(save);
        Assert.Contains("selectedContacts: contacts", save);
        // Every selected doctor is built as { contactId, accountId, accountContactLinkId } — never with products.
        Assert.DoesNotMatch(@"\bproducts\b", js);
        Assert.Matches(@"selectedContacts\[[^\]]+\] = \{ contactId: [^}]*accountContactLinkId: [^}]*\}", js);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}

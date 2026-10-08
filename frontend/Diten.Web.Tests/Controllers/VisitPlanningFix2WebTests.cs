using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-FIX-2 — on the REAL scripts: (D9) the planning form's EDIT sends no target list (CRM keeps the selection —
/// null = unchanged) while CREATE still starts with empty lists, and the Targets "save" path still sends full lists;
/// (F-1) the Targets doctor search is Turkish-insensitive (tr lower-casing + i / ı folding + case-insensitive regex).
/// </summary>
public sealed class VisitPlanningFix2WebTests
{
    [Fact]
    public void The_edit_form_sends_no_target_lists_and_the_create_form_still_does()
    {
        var js = Asset("form.js");

        Assert.Contains("const buildPayload = isEdit => isEdit", js);
        var edit = Between(js, "const buildPayload = isEdit => isEdit", ": {");
        Assert.Contains("cyclePeriodId", edit);
        Assert.DoesNotContain("selected", edit);  // no target list at all on edit

        var create = Between(js, ": {", "};");
        Assert.Contains("selectedAccountIds: []", create);
        Assert.Contains("selectedPharmacyIds: []", create);
        Assert.Contains("selectedContacts: []", create);
        Assert.Contains("buildPayload(mode === 'edit' && !!sessionId)", js);

        // The Targets tab's save keeps sending the full lists (unchanged path).
        var details = Asset("details.js");
        Assert.Contains("selectedAccountIds: targetAccounts.map(a => a.id), selectedPharmacyIds: Object.keys(selectedPharmacies)", details);
    }

    [Fact]
    public void The_doctor_search_is_turkish_insensitive()
    {
        var js = Asset("details.js");

        var pattern = Between(js, "const trSearchPattern = term =>", ";");
        Assert.Contains("toLocaleLowerCase('tr')", pattern);   // İ → i, I → ı
        Assert.Contains(".replace(/[iı]/g, '[iıIİ]')", pattern); // every i letter matches all four
        Assert.Contains(@"'\\$&'", pattern);                     // the term is escaped (literal text)

        // WP-VP-4J — the plain table: the pattern as a case-insensitive regex over the Turkish-lower-cased name / specialty.
        Assert.Contains("docTerm ? new RegExp(trSearchPattern(docTerm), 'i') : null", js);
        Assert.Contains("re.test(String(row.name || '').toLocaleLowerCase('tr'))", js);
        Assert.DoesNotContain("contactsDt", js);
    }

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return text[from..(to + end.Length)];
    }

    private static string Asset(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning", file));

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}

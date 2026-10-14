using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Quarantine;

/// <summary>
/// WP-MVP1-FUB2 — the Kayıt Detayı side of QUARANTINE 1.2.0:
/// <list type="bullet">
/// <item>FU-0175-ITEM-01: every HistoryAction of the contract (now with <c>NoteUpdated</c>) has its label, and a NoteUpdated row shows
/// its <c>noteChange</c> (note length and attachment count before → after);</item>
/// <item>FU-0175-ITEM-03: while the hold has a submitted decision (<c>activeDecision</c>) "Notu düzenle" is not drawn and the lock
/// explanation is shown in its place (only to a holder of <c>quarantine.holds.review</c>, UAS-001); the lock lifts with the
/// decision.</item>
/// </list>
/// </summary>
public sealed class HoldNoteLockWebTests
{
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string DetailsJs = Path.Combine(Web, "wwwroot", "assets", "js", "Quarantine", "details.js");

    private static string Contract()
        => File.ReadAllText(Directory.EnumerateFiles(Path.Combine(TraceViewRenderer.RepoRoot(), "docs"), "quarantine.openapi.yaml", SearchOption.AllDirectories).Single());

    [Fact]
    public void Every_history_action_of_the_contract_has_its_label()
    {
        var line = Regex.Match(Contract(), @"(?m)^\s*HistoryAction: \{ type: string, enum: \[(?<v>[^\]]+)\] \}").Groups["v"].Value;
        var actions = line.Split(',').Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
        Assert.Contains("NoteUpdated", actions);
        var keys = Diten.Web.Views.Quarantine.Quarantine.Keys.ToHashSet(StringComparer.Ordinal);
        Assert.All(actions, a => Assert.True(keys.Contains($"Quarantine.History.{a}"), $"no label for history action {a}"));
    }

    [Fact]
    public void A_NoteUpdated_row_shows_the_note_and_attachment_change()
    {
        var details = File.ReadAllText(DetailsJs);
        Assert.Contains("h.noteChange ? `<div class=\"small mt-1\">${esc(t('Quarantine.Details.History.NoteChange', h.noteChange.previousNoteLength, h.noteChange.noteLength, h.noteChange.previousAttachmentCount, h.noteChange.attachmentCount))}</div>` : ''", details);
        Assert.Contains("Quarantine.Details.History.NoteChange", Diten.Web.Views.Quarantine.Quarantine.Keys);
    }

    [Fact]
    public void While_a_decision_is_open_the_note_edit_is_not_drawn_and_the_lock_explanation_is()
    {
        var details = File.ReadAllText(DetailsJs);
        Assert.Contains("const noteLocked = !!h.activeDecision;", details);
        Assert.Contains("show(ui.byId('qr-d-note-edit'), ui.can('STARTREVIEW') && open && !noteLocked);", details);
        Assert.Contains("show(ui.byId('qr-d-note-locked'), ui.can('STARTREVIEW') && open && noteLocked);", details);

        var view = File.ReadAllText(Path.Combine(Web, "Views", "Quarantine", "Details.cshtml"));
        var at = view.IndexOf("id=\"qr-d-note-locked\"", StringComparison.Ordinal);
        Assert.True(at > 0, "qr-d-note-locked is not in the view");
        var guard = view.LastIndexOf("@if (Perms.Has(\"quarantine.holds.review\"))", at, StringComparison.Ordinal);
        Assert.True(guard > 0 && view.IndexOf('}', guard) > at, "qr-d-note-locked is outside the quarantine.holds.review guard");
        var element = view[at..view.IndexOf("</div>", at, StringComparison.Ordinal)];
        Assert.Contains("d-none", view[view.LastIndexOf('<', at)..at]); // hidden until the script decides
        Assert.Contains("@Localizer[\"Quarantine.Details.Quality.NoteLocked\"]", element);
        Assert.Contains("Quarantine.Details.Quality.NoteLocked", Diten.Web.Views.Quarantine.Quarantine.Keys);
    }
}

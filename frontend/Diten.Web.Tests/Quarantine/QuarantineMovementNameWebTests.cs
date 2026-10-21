using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Quarantine;

/// <summary>
/// WP-MVP1-NAMES1 — the hold / blocked page's movement trail shows who posted each movement with the name recorded at the posting
/// (<c>MovementLink.byUserName</c>, filled by the service now); without one the cell says the 7-language "unknown user" — never an empty cell
/// that reads as "nobody", never an id.
/// </summary>
public sealed class QuarantineMovementNameWebTests
{
    [Fact]
    public void The_movement_row_shows_the_recorded_name_else_unknown_user()
    {
        var details = File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Quarantine", "details.js"));
        Assert.Contains("<td class=\"small\">${esc(m.byUserName || t('Quarantine.Details.Moves.UnknownUser'))}</td>", details, StringComparison.Ordinal);
        Assert.DoesNotContain("m.byUserName || ''", details, StringComparison.Ordinal);
        Assert.Contains("Quarantine.Details.Moves.UnknownUser", Diten.Web.Views.Quarantine.Quarantine.Keys);
    }
}

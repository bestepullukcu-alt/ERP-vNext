namespace Diten.Web.Models.CRM;

/// <summary>MOD-0155 FU02 — the Visit Report execution calendar page model. The permission flags gate the inline
/// mark-done / report / amend affordances; the CrmService runtime remains the authoritative permission layer.</summary>
public sealed class VisitExecutionIndexViewModel
{
    public bool CanRecord { get; set; }
    public bool CanAmend { get; set; }
}

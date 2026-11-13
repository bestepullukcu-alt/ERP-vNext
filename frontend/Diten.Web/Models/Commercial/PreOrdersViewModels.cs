namespace Diten.Web.Models.Commercial;

/// <summary>
/// WP-VW-W6 (W6-WEB-a) — what the pre-order screens may draw for the caller (MOD-0168 §14). The CRM decides every write
/// again; these flags only decide what is RENDERED (UAS-001: without the page's key only the notice — no skeleton, no
/// redirect). The per-order abilities (edit / submit / decide / cancel / copy …) come with the order itself (<c>can</c>).
/// </summary>
public sealed class PreOrderPageViewModel
{
    /// <summary><c>commercial.pre-orders.read</c> (own + team) or <c>.read-all</c> (sales operations).</summary>
    public bool CanRead { get; set; }

    /// <summary><c>commercial.pre-orders.read-all</c>: every order of the tenant.</summary>
    public bool CanReadAll { get; set; }

    public bool CanCreate { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanApprove { get; set; }
    public bool CanCancel { get; set; }
    public bool CanRecordDepotResponse { get; set; }
    public bool CanRetryTransmission { get; set; }
    public bool CanExport { get; set; }

    /// <summary>Reads more than one rep's orders (a manager / an approver / sales operations): the rep column and filter.</summary>
    public bool SeesTeam { get; set; }

    /// <summary>The page this view is (<c>list</c> · <c>create</c> · <c>edit</c> · <c>details</c> · <c>approve</c> · <c>link</c> · <c>document</c>).</summary>
    public string Screen { get; set; } = "list";

    /// <summary>The order of a per-order screen; null for the list and a new draft.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>A visit the new draft is opened from (optional, MOD-0168 §4.1 #13).</summary>
    public Guid? VisitId { get; set; }

    /// <summary>The e-mail link's token (screen C-link only): it is sent back with the decision, never stored by the page.</summary>
    public string? LinkToken { get; set; }
}

/// <summary>WP-VW-W6 (W6-WEB-a) — the tenant's pre-order settings form (MOD-0168 §4.3).</summary>
public sealed class PreOrderSettingsViewModel
{
    /// <summary><c>commercial.pre-order-settings.read</c> or <c>.update</c>.</summary>
    public bool CanRead { get; set; }

    /// <summary><c>commercial.pre-order-settings.update</c>: the form is editable and the save button exists.</summary>
    public bool CanUpdate { get; set; }
}

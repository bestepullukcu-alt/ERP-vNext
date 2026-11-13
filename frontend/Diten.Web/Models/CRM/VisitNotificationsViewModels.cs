namespace Diten.Web.Models.CRM;

/// <summary>WP-VW-W5 (W5-WEB) — the sent visit e-mails page's flags (UAS-001: without <see cref="CanRead"/> only the notice).</summary>
public sealed class VisitNotificationsViewModel
{
    /// <summary>crm.visit-notifications.read (own + team) or .settings.manage (the tenant).</summary>
    public bool CanRead { get; set; }

    /// <summary>crm.visit-notifications.settings.manage: the settings tab and card exist (otherwise not rendered at all).</summary>
    public bool CanManageSettings { get; set; }
}

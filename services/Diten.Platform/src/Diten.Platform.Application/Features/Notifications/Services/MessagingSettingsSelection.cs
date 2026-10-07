using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>
/// BL-499 (2) — the ONE rule that picks the messaging settings a mail is sent with. The settings resolver (which the
/// queue handler and the retry job ask first) and the SMTP provider (which reads the settings again to connect) both
/// apply it, so they can no longer disagree. Before this the provider fell back to the platform default whenever the
/// tenant's own row was disabled, whatever the tenant's <see cref="NotificationFallbackPolicy"/> said.
///
/// <list type="bullet">
///   <item>No tenant row: the platform default (unchanged).</item>
///   <item>The tenant row enabled: the tenant row (unchanged).</item>
///   <item>The tenant row disabled, policy <see cref="NotificationFallbackPolicy.UsePlatformDefault"/>: the platform default.</item>
///   <item>The tenant row disabled, policy <see cref="NotificationFallbackPolicy.DisableSending"/>: nothing is sent,
///     refused as <see cref="ReasonTenantSendingDisabled"/>.</item>
///   <item>The tenant row disabled, policy <see cref="NotificationFallbackPolicy.FailFast"/>: nothing is sent,
///     refused as <see cref="ReasonTenantSettingsDisabled"/>.</item>
///   <item>A platform default that is missing or disabled when it is needed: refused as
///     <see cref="ReasonPlatformDefaultUnavailable"/>.</item>
/// </list>
/// </summary>
public static class MessagingSettingsSelection
{
    public const string ReasonTenantSendingDisabled = "TENANT_SENDING_DISABLED";
    public const string ReasonTenantSettingsDisabled = "TENANT_SETTINGS_DISABLED";
    public const string ReasonPlatformDefaultUnavailable = "PLATFORM_DEFAULT_UNAVAILABLE";

    /// <summary>C-FIX1 2 — a stored policy value this code does not know (3, 99, …). Refused by its own name rather than
    /// one of the two disabling ones: neither says what happened, and the operator must see that the row is unreadable.</summary>
    public const string ReasonTenantFallbackPolicyUnknown = "TENANT_FALLBACK_POLICY_UNKNOWN";

    public static (TenantMessagingSettings? Settings, string? RefusalCode) Select(
        TenantMessagingSettings? tenantRow, Func<TenantMessagingSettings?> platformDefault)
    {
        if (tenantRow is { IsDeleted: false })
        {
            if (tenantRow.IsEnabled)
            {
                return (tenantRow, null);
            }

            switch (tenantRow.FallbackPolicy)
            {
                case NotificationFallbackPolicy.UsePlatformDefault:
                    break; // the only value that reaches the platform's mailbox
                case NotificationFallbackPolicy.DisableSending:
                    return (null, ReasonTenantSendingDisabled);
                case NotificationFallbackPolicy.FailFast:
                    return (null, ReasonTenantSettingsDisabled);
                default:
                    // C-FIX1 2 — closed by default: an unknown value never opens the platform's mailbox.
                    return (null, ReasonTenantFallbackPolicyUnknown);
            }
        }

        var fallback = platformDefault();
        return fallback is { IsDeleted: false, IsEnabled: true }
            ? (fallback, null)
            : (null, ReasonPlatformDefaultUnavailable);
    }

    /// <summary>The reader's sentence for a refusal code (logs and the operator's error field; never shown to a tenant).</summary>
    public static string Describe(string refusalCode) => refusalCode switch
    {
        ReasonTenantSendingDisabled => "Tenant messaging settings are disabled and the tenant's policy is not to send.",
        ReasonTenantSettingsDisabled => "Tenant messaging settings are disabled and fallback is not allowed.",
        ReasonTenantFallbackPolicyUnknown => "Tenant messaging settings are disabled and their fallback policy is not a known value.",
        _ => "Platform default messaging settings were not found or are disabled."
    };
}

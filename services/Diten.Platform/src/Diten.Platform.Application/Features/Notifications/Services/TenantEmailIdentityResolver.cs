using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>
/// BL-454 — who an e-mail is from, as far as its reader is concerned: the tenant's display name, the language the
/// tenant reads, the sender name the tenant chose for itself (if it chose one) and where a reply goes.
///
/// <para>Nothing here is a secret or a transport setting. Host, port, credential reference and the sender ADDRESS
/// stay in <c>TenantMessagingSettings</c> and never travel through this record — it is also what the internal
/// <c>email-identity</c> endpoint hands to AuthService.</para>
/// </summary>
public sealed record TenantEmailIdentity(
    string? DisplayName,
    string Language,
    /// <summary>Only a name written on the tenant's OWN settings row. The platform-default row's name is not it.</summary>
    string? SenderName,
    string? ReplyToEmail)
{
    /// <summary>A platform e-mail: no tenant behind it.</summary>
    public static TenantEmailIdentity Platform { get; } =
        new(null, TenantNotificationLocaleResolver.PlatformDefaultLocale, null, null);
}

public interface ITenantEmailIdentityResolver
{
    /// <summary>Null only when the tenant does not exist. <see cref="Guid.Empty"/> is the platform itself.</summary>
    Task<TenantEmailIdentity?> ResolveAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class TenantEmailIdentityResolver : ITenantEmailIdentityResolver
{
    private readonly ITenantRegistryRepository _tenants;
    private readonly ITenantMessagingSettingsRepository _settings;
    private readonly INotificationLocaleResolver _locale;

    public TenantEmailIdentityResolver(
        ITenantRegistryRepository tenants,
        ITenantMessagingSettingsRepository settings,
        INotificationLocaleResolver locale)
    {
        _tenants = tenants;
        _settings = settings;
        _locale = locale;
    }

    public async Task<TenantEmailIdentity?> ResolveAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            var platformDefault = await _settings.GetPlatformDefaultAsync(ct);
            return TenantEmailIdentity.Platform with { ReplyToEmail = SingleAddress(platformDefault?.ReplyToEmail) };
        }

        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
        {
            return null;
        }

        /*
         * The SAME row the SMTP provider sends with: the tenant's own row when it is enabled, the platform default
         * otherwise. The reply address follows that row — a footer promising a reply address the message does not
         * carry would be a lie. The sender NAME is taken only from the tenant's own row: the platform default's
         * name belongs to the platform, and reading it as the tenant's would put one name on every tenant's mail.
         */
        var own = await _settings.GetByTenantIdAsync(tenantId, ct);
        var platformRow = await _settings.GetPlatformDefaultAsync(ct);
        // C-FIX1 K6 — the ONE selection rule (MessagingSettingsSelection), not a copy: a tenant whose policy refuses the
        // platform's mailbox gets no reply address from it either.
        var (effective, _) = MessagingSettingsSelection.Select(own, () => platformRow);
        var ownIsLive = effective is not null && ReferenceEquals(effective, own);

        return new TenantEmailIdentity(
            Blank(tenant.DisplayName) ?? Blank(tenant.Name),
            await _locale.ResolveAsync(tenantId, null, ct),
            ownIsLive ? Blank(own!.SenderName) : null,
            effective is { IsDeleted: false, IsEnabled: true } ? SingleAddress(effective.ReplyToEmail) : null);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? SingleAddress(string? value) =>
        Diten.BuildingBlocks.Email.EmailAddressText.IsSingleAddress(Blank(value)) ? Blank(value) : null;
}

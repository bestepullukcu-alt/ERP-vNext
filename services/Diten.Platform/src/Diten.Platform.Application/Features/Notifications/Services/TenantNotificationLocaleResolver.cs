using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>
/// The tenant's own configured language, with the chain written down.
///
/// <para><b>The chain, and why each link is where it is.</b></para>
/// <list type="number">
///   <item><description>
///     <b>What the caller asked for.</b> A caller that knows the recipient's language knows more than the tenant
///     record does. The tenant-lifecycle mappers are exactly this case — they carry the language from the
///     provisioning envelope — and this link is what keeps their behaviour unchanged.
///   </description></item>
///   <item><description>
///     <b><c>Tenant.Settings.Language</c>.</b> The runtime override. <c>Tenant.cs</c> says so in its own comment:
///     "tenant profile defaults — TenantSettings holds runtime overrides". It is what the tenant settings screen
///     writes, so it is the most recent statement of intent.
///   </description></item>
///   <item><description>
///     <b><c>Tenant.DefaultLanguage</c>.</b> The profile default, set at registration. Reached when settings were
///     never touched.
///   </description></item>
///   <item><description>
///     <b><c>"en"</c>.</b> The platform floor — the same value <see cref="NotificationParsing.NormalizeLocale"/>
///     already produces for blank input. Reached when the tenant cannot be read at all.
///   </description></item>
/// </list>
///
/// <para><b>What is deliberately NOT a link: the recipient's own preference.</b> It does not exist. Measured:
/// <c>Diten.AuthService.Domain/Entities/User.cs</c> carries no Locale/Language/Culture field, and the
/// <c>internal/users/contacts</c> endpoint WC-4 added returns id, display name and email — nothing else. Inventing
/// a per-user language here would mean guessing, and a guessed language is worse than the tenant's declared one:
/// the tenant at least chose theirs. When that field is added, it becomes link 1.5 and this is the only class that
/// changes.</para>
///
/// <para><b>Known remaining gap, not closed by this ticket.</b> A tenant whose language is one the platform never
/// seeded a template for (say <c>"de"</c>) resolves to <c>"de"</c>, finds no template, and the queue handler
/// answers 404 with NO dispatch record — silence, again. The seven seeded languages
/// (en/tr/fr/es/zh/ar/ru) cover the product's supported set, and <c>GetBestActiveByKeyAsync</c> already narrows
/// <c>tr-TR</c> to <c>tr</c>, so this only bites a tenant configured outside the supported set. Closing it means a
/// final platform-default-locale step inside <c>NotificationTemplateRepository.GetBestActiveByKeyAsync</c>, which
/// is shared notification infrastructure every producer relies on — out of scope here, reported instead.</para>
/// </summary>
public sealed class TenantNotificationLocaleResolver : INotificationLocaleResolver
{
    /// <summary>The floor. Identical to what <see cref="NotificationParsing.NormalizeLocale"/> yields for blank.</summary>
    public const string PlatformDefaultLocale = "en";

    private readonly ITenantRegistryRepository _tenants;
    private readonly ILogger<TenantNotificationLocaleResolver> _logger;

    public TenantNotificationLocaleResolver(
        ITenantRegistryRepository tenants,
        ILogger<TenantNotificationLocaleResolver> logger)
    {
        _tenants = tenants;
        _logger = logger;
    }

    public async Task<string> ResolveAsync(Guid tenantId, string? requested, CancellationToken ct = default)
    {
        // Link 1 — the caller knows the recipient. Short-circuits before any I/O.
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return NotificationParsing.NormalizeLocale(requested);
        }

        if (tenantId == Guid.Empty)
        {
            // Platform-context dispatch (no tenant to ask). Not an error; there is simply nobody whose language
            // this could be.
            return PlatformDefaultLocale;
        }

        try
        {
            var tenant = await _tenants.GetByIdAsync(tenantId, ct);
            if (tenant is null)
            {
                _logger.LogWarning(
                    "notification.locale.tenant_not_found TenantId={TenantId}. Falling back to '{Locale}'.",
                    tenantId, PlatformDefaultLocale);
                return PlatformDefaultLocale;
            }

            // Link 2, then link 3.
            var candidate = FirstNonBlank(tenant.Settings?.Language, tenant.DefaultLanguage);
            return candidate is null
                ? PlatformDefaultLocale
                : NotificationParsing.NormalizeLocale(candidate);
        }
        catch (Exception ex)
        {
            /*
             * Link 4. A registry read that fails must not become a notification that is never sent — this class
             * sits in front of every dispatch, and throwing here would turn one unreachable collection into total
             * notification silence.
             */
            _logger.LogWarning(
                ex,
                "notification.locale.resolve_failed TenantId={TenantId}. Falling back to '{Locale}'.",
                tenantId, PlatformDefaultLocale);
            return PlatformDefaultLocale;
        }
    }

    private static string? FirstNonBlank(params string?[] candidates)
        => candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
}

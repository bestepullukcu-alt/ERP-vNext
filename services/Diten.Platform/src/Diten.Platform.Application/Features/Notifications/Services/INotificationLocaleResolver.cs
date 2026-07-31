namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>
/// Answers the one question every dispatch has to answer before a template can be looked up: <b>which language does
/// this recipient read?</b>
///
/// <para><b>Why this exists as a seam rather than a line in the adapter.</b> The answer has a fallback chain, the
/// chain is going to grow (a per-user preference is the obvious next link), and until WC-4 nobody owned it — the
/// adapter passed <c>string.Empty</c> and the validator refused it, so MOD-0024 sent nothing at all. Naming the
/// question puts the chain in one testable place instead of at every call site.</para>
/// </summary>
public interface INotificationLocaleResolver
{
    /// <summary>
    /// Resolves the locale for a dispatch. Never throws and never returns blank — an unreachable tenant registry
    /// yields the platform floor, because a notification in the wrong language still reaches somebody and a
    /// refused one does not.
    /// </summary>
    /// <param name="tenantId">Whose language is being asked about.</param>
    /// <param name="requested">
    /// What the caller asked for, if anything. A caller that genuinely knows the recipient's language (the tenant
    /// lifecycle mappers do) passes it and wins outright; a caller with no such knowledge passes <c>null</c> and
    /// gets the tenant's own configured language.
    /// </param>
    Task<string> ResolveAsync(Guid tenantId, string? requested, CancellationToken ct = default);
}

using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Account;

/// <summary>
/// WP-VP-2B (mobile R1 / R2) — "how many ACTIVE contacts does an account have", with ONE rule for every surface
/// (<see cref="RelationshipLifecycle.IsActiveLink"/>): the link is not deleted and not closed (ended / inactive, trimmed,
/// case-insensitive) and its contact still exists (not soft-deleted) — exactly what the account's <c>/contacts</c>
/// projection lists as active. Only a COUNT is exposed (under <c>crm.account.read</c>); no contact data.
/// <para>Bounded cost: a page costs two reads whatever its size (its links by <c>AccountId IN</c>, then those links'
/// contacts by id); the tenant-wide filter set costs one deleted-contact id read + one grouping aggregation.</para>
/// </summary>
public static class AccountActiveContacts
{
    public const string InvalidFilterCode = "invalid_has_active_contacts";

    /// <summary>Active-contact count per account of a page (accounts with none are absent → 0).</summary>
    public static async Task<IReadOnlyDictionary<Guid, int>> CountForPageAsync(
        IAccountContactLinkRepository links,
        IContactRepository contacts,
        Guid tenantId,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0) return new Dictionary<Guid, int>();

        var pageLinks = (await links.ListByAccountIdsAsync(tenantId, accountIds, cancellationToken))
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && !RelationshipLifecycle.IsClosed(l.Status))
            .ToList();
        if (pageLinks.Count == 0) return new Dictionary<Guid, int>();

        var alive = (await contacts.ListByIdsAsync(
                tenantId, pageLinks.Select(l => l.ContactId).Distinct().ToList(), cancellationToken))
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => c.Id)
            .ToHashSet();

        return pageLinks
            .Where(l => RelationshipLifecycle.IsActiveLink(l, alive.Contains(l.ContactId)))
            .GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>The tenant's accounts that have at least one active contact (the <c>hasActiveContacts</c> set).</summary>
    public static async Task<IReadOnlyCollection<Guid>> AccountIdsWithActiveContactsAsync(
        IAccountContactLinkRepository links,
        IContactRepository contacts,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var deleted = await contacts.ListDeletedIdsAsync(tenantId, cancellationToken);
        return await links.ListAccountIdsWithActiveLinksAsync(tenantId, deleted, cancellationToken);
    }

    /// <summary><c>hasActiveContacts</c>: absent ⇒ (true, null); "true"/"false" (case-insensitive) ⇒ (true, value);
    /// anything else ⇒ (false, null) — the caller answers 400 <see cref="InvalidFilterCode"/>.</summary>
    public static (bool Valid, bool? Value) ParseFilter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (true, null);
        return raw.Trim().ToLowerInvariant() switch
        {
            "true" => (true, true),
            "false" => (true, false),
            _ => (false, null)
        };
    }

    /// <summary>ANDs the filter onto an existing inclusion scope (null = no scope). <c>true</c> narrows the scope to the
    /// accounts with active contacts; <c>false</c> removes them — from the scope when there is one, otherwise as an
    /// exclusion set (<c>Id NIN</c>). Absent leaves both untouched.</summary>
    public static (IReadOnlyCollection<Guid>? Scope, IReadOnlyCollection<Guid>? Excluded) Apply(
        bool? hasActiveContacts, IReadOnlyCollection<Guid>? scope, IReadOnlyCollection<Guid> withActive)
    {
        if (hasActiveContacts is null) return (scope, null);
        var set = withActive as ISet<Guid> ?? withActive.ToHashSet();
        if (hasActiveContacts.Value)
        {
            return (scope is null ? set.ToList() : scope.Where(set.Contains).ToList(), null);
        }

        return scope is null ? (null, set.ToList()) : (scope.Where(id => !set.Contains(id)).ToList(), null);
    }
}

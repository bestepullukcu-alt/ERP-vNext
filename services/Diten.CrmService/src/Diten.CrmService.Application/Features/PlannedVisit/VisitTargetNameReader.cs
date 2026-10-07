using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.PlannedVisit;

/// <summary>
/// WP-VP-2 (B-8, mobile D1) — the display names of visit targets, read at READ time and never copied onto a record.
/// One bulk read per master for the whole page (accounts by id list, contacts by id list), never one read per row. A
/// target that cannot be found keeps its id and gets a null name; <c>Inactive</c> marks a target whose master status is
/// not <c>active</c> (passive or archived).
/// </summary>
public sealed class VisitTargetNameReader
{
    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contacts;

    public VisitTargetNameReader(IAccountRepository accounts, IContactRepository contacts)
    {
        _accounts = accounts;
        _contacts = contacts;
    }

    public async Task<VisitTargetNames> ReadAsync(
        Guid tenantId, IEnumerable<Guid?> accountIds, IEnumerable<Guid?> contactIds, CancellationToken cancellationToken)
    {
        var aIds = accountIds.OfType<Guid>().Where(id => id != Guid.Empty).Distinct().ToList();
        var cIds = contactIds.OfType<Guid>().Where(id => id != Guid.Empty).Distinct().ToList();

        var accounts = aIds.Count == 0
            ? new Dictionary<Guid, NamedTarget>()
            : (await _accounts.ListByIdsAsync(tenantId, aIds, cancellationToken))
                .GroupBy(a => a.Id)
                .ToDictionary(g => g.Key, g => new NamedTarget(g.First().AccountName, IsInactive(g.First().Status)));
        var contacts = cIds.Count == 0
            ? new Dictionary<Guid, NamedTarget>()
            : (await _contacts.ListByIdsAsync(tenantId, cIds, cancellationToken))
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => new NamedTarget(g.First().DisplayName, IsInactive(g.First().Status)));

        return new VisitTargetNames(accounts, contacts);
    }

    private static bool IsInactive(string? status) => IsInactiveStatus(status);

    /// <summary>The one "inactive target" rule (WP-VP-2; shared since WP-VP-3D): a master whose status is set and is not
    /// <c>active</c> (passive or archived).</summary>
    public static bool IsInactiveStatus(string? status)
        => !string.IsNullOrWhiteSpace(status) && !string.Equals(status.Trim(), "active", StringComparison.OrdinalIgnoreCase);
}

public sealed record NamedTarget(string? Name, bool Inactive);

/// <summary>The names of one page of targets. <see cref="For"/> answers a visit's target / account / contact names.</summary>
public sealed class VisitTargetNames
{
    public static readonly VisitTargetNames Empty = new(new Dictionary<Guid, NamedTarget>(), new Dictionary<Guid, NamedTarget>());

    private readonly IReadOnlyDictionary<Guid, NamedTarget> _accounts;
    private readonly IReadOnlyDictionary<Guid, NamedTarget> _contacts;

    public VisitTargetNames(IReadOnlyDictionary<Guid, NamedTarget> accounts, IReadOnlyDictionary<Guid, NamedTarget> contacts)
    {
        _accounts = accounts;
        _contacts = contacts;
    }

    public string? Account(Guid? id) => id is { } a && _accounts.TryGetValue(a, out var n) ? Blank(n.Name) : null;

    public string? Contact(Guid? id) => id is { } c && _contacts.TryGetValue(c, out var n) ? Blank(n.Name) : null;

    /// <summary>A contact target is named by the doctor, an account / pharmacy target by the institution.</summary>
    public (string? Target, string? Account, string? Contact, bool TargetInactive) For(
        string targetType, Guid targetId, Guid? accountId, Guid? contactId)
    {
        var isContact = string.Equals(targetType, PlannedVisitTargetType.Contact, StringComparison.Ordinal);
        var target = isContact
            ? (_contacts.TryGetValue(contactId ?? targetId, out var c) ? c : null)
            : (_accounts.TryGetValue(targetId, out var a) ? a : null);
        return (Blank(target?.Name), Account(accountId ?? (isContact ? null : targetId)), Contact(contactId),
            target?.Inactive ?? false);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

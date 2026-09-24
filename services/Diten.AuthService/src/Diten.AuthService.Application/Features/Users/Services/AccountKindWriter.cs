using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>What a kind change was: the value before and the value after. Null from <see cref="AccountKindWriter.Apply"/> means "no change".</summary>
public sealed record AccountKindChange(AccountKind Previous, AccountKind Next);

/// <summary>
/// WP-AUTH-USER-KIND-UPDATE-01 — THE one write path for an EXISTING account's kind.
///
/// <para>Two doors reach it: <c>POST api/users/{id}/account-kind</c> (SetAccountKindCommandHandler) and
/// <c>PUT api/users/{id}</c> (UpdateUserCommandHandler). Both go through <see cref="Apply"/> to mutate the entity and
/// through <see cref="RecordAsync"/> to write the SAME <c>authAuditLogs</c> row. Two writers of one field that carries
/// a permission and an audit trail is how they end up disagreeing — AccountKindCreationPathsGuardTests counts that
/// neither handler calls <c>User.SetAccountKind</c> itself.</para>
///
/// <para>Persisting stays with the caller, between the two calls: UpdateUser writes the profile and the kind in ONE
/// tenant-scoped replace (the repository replaces the whole document, so two sequential writes of two stale copies
/// would clobber each other). The audit row is written only after the entity is persisted.</para>
/// </summary>
public sealed class AccountKindWriter
{
    public const string AuditEventName = "user_account_kind_changed";

    private readonly IRbacAuditRecorder _audit;

    public AccountKindWriter(IRbacAuditRecorder audit)
    {
        _audit = audit;
    }

    /// <summary>True only for a defined enum NAME (case-insensitive, trimmed). Defense in depth behind the validators.</summary>
    public static bool TryParse(string? name, out AccountKind kind)
    {
        kind = AccountKind.Unknown;
        return !string.IsNullOrWhiteSpace(name)
               && Enum.GetNames<AccountKind>().Any(n => string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase))
               && Enum.TryParse(name.Trim(), ignoreCase: true, out kind)
               && Enum.IsDefined(kind);
    }

    /// <summary>
    /// Sets the kind on the entity when it differs and reports the change; returns null (and touches nothing — not
    /// even <c>UpdatedAt</c>) when the account already has that kind, so the same kind twice is no write and no row.
    /// </summary>
    public AccountKindChange? Apply(User user, AccountKind newKind)
    {
        var previous = user.AccountKind;
        if (previous == newKind)
        {
            return null;
        }

        user.SetAccountKind(newKind);
        return new AccountKindChange(previous, newKind);
    }

    /// <summary>The audit row for a change the caller has already persisted. The actor is stamped by the recorder.</summary>
    public Task RecordAsync(User user, AccountKindChange change, Guid tenantId, string? correlationId, CancellationToken ct)
        => _audit.RecordAsync(AuditEventName, tenantId, new
        {
            targetUserId = user.Id,
            tenantId,
            previousKind = change.Previous.ToString(),
            newKind = change.Next.ToString(),
            correlationId
        }, ct);
}

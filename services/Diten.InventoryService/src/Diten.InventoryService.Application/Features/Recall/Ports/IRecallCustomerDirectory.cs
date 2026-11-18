namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>MOD-0177-FU01 (pack §8.6) — what CRM-ACCOUNT-LOOKUP U-1 said about one account.</summary>
public enum RecallCustomerOutcome
{
    /// <summary>200 with a valid body: the account's name and code.</summary>
    Resolved,

    /// <summary>404 — no such account, deleted, or another tenant's (CRM answers the three alike).</summary>
    NotFound,

    /// <summary>401 / 403 — the caller may not read it (no <c>crm.account.lookup-validation</c>); retrying does not help.</summary>
    Forbidden,

    /// <summary>Anything else: invalid body, 400, 5xx, timeout, budget spent, breaker open, unreachable.</summary>
    Unresolved
}

/// <summary>The time rule of a resolution (pack §8.6): a screen page (≤ 25 accounts, 3 s in total) or an export (every account, breaker).</summary>
public enum RecallLookupMode
{
    Page,
    Export
}

/// <summary>One account's answer; <see cref="Code"/> and <see cref="Name"/> only when <see cref="Outcome"/> is Resolved.</summary>
public sealed record RecallCustomerName(RecallCustomerOutcome Outcome, string? Code = null, string? Name = null)
{
    public static readonly RecallCustomerName Unresolved = new(RecallCustomerOutcome.Unresolved);
}

/// <summary>
/// MOD-0177-FU01 (pack §8.6) — the CRM name / code of CRM-keyed recall customers, read NOW (no cache) with the caller's token through the
/// Gateway. Never throws for a CRM failure: every requested account is in the answer (an unanswered one is Unresolved). Page mode takes at
/// most 25 accounts (<c>RecallCustomerDisplay.PageSize</c>); more is a programming error.
/// </summary>
public interface IRecallCustomerDirectory
{
    Task<IReadOnlyDictionary<Guid, RecallCustomerName>> ResolveAsync(IReadOnlyCollection<Guid> accountIds, RecallLookupMode mode,
        CancellationToken cancellationToken = default);
}

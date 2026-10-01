namespace Diten.AuthService.Application.Common.Interfaces;

public enum UserQuotaOutcome
{
    /// <summary>Platform took one seat of the tenant's <c>users.max</c>.</summary>
    Consumed,

    /// <summary>Platform refused: the plan's user limit is reached. Nothing may be written.</summary>
    LimitExceeded,

    /// <summary>
    /// Platform could not answer (down, slow, no quota row for the tenant, any other refusal). Owner decision (BL-459):
    /// the create goes ahead — the quota is enforced where it can be read, never at the cost of locking a tenant out of
    /// adding people — and a <c>quota_unavailable</c> warning is logged.
    /// </summary>
    Unavailable
}

/// <summary>What Platform answered; <see cref="Limit"/>/<see cref="Current"/> only on <see cref="UserQuotaOutcome.LimitExceeded"/>.</summary>
public sealed record UserQuotaDecision(UserQuotaOutcome Outcome, decimal? Limit = null, decimal? Current = null);

/// <summary>
/// BL-459 — the tenant's plan user limit (<c>users.max</c>) through Platform's internal quota contract
/// (<c>POST /api/internal/quotas/consume</c>, X-Internal-Api-Key), asked BEFORE a user is written or switched back on.
/// <para>F1 — Platform answers from its live count of this tenant's Active + Invited users (<c>internal/users/counts</c>);
/// the call reserves nothing, so there is no release: a deleted or deactivated user simply leaves the count.</para>
/// NEVER throws.
/// </summary>
public interface IUserQuotaClient
{
    Task<UserQuotaDecision> TryConsumeUserSeatAsync(Guid tenantId, string operationReference, CancellationToken ct);
}

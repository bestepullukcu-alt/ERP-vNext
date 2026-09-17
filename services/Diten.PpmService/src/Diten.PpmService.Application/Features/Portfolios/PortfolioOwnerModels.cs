using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Portfolios;

public enum PortfolioAuthorityOutcome { Unavailable, Allowed, Denied, NotFound }

// Internal consumer evidence only. No request body or untrusted caller supplies this context.
public sealed record PortfolioAuthorityScope(Guid TenantId, Guid ActorId, Guid? PortfolioId,
    string Operation, int? Version = null, Guid? TargetUserId = null, Guid? RequestId = null,
    int? ExpectedVersion = null, Guid? ExpectedAssignmentId = null, string? Reason = null,
    string? VisibilityPolicyKey = null, string? Code = null, string? Name = null,
    string? Description = null, string? CapacityAllocationDescription = null,
    string? Search = null, int? Limit = null, Guid? RecordTenantId = null,
    Guid? CreatorId = null, Guid? CurrentOwnerUserId = null,
    PortfolioLifecycleState? LifecycleState = null,
    PortfolioTemporaryNonProductionAccessBinding? TemporaryNonProductionAccessBinding = null);

public sealed record PortfolioAuthorityEvidence(PortfolioAuthorityScope Scope,
    PortfolioAuthorityOutcome Outcome, string PolicyVersion, DateTime IssuedAtUtc, DateTime ExpiresAtUtc,
    string? ResolvedVisibilityPolicyKey = null)
{
    public bool IsBoundTo(PortfolioAuthorityScope scope) => Scope == scope &&
        !string.IsNullOrWhiteSpace(PolicyVersion) &&
        IssuedAtUtc.Kind == DateTimeKind.Utc && ExpiresAtUtc.Kind == DateTimeKind.Utc &&
        IssuedAtUtc <= DateTime.UtcNow && ExpiresAtUtc > DateTime.UtcNow && ExpiresAtUtc > IssuedAtUtc;
}
public enum PortfolioOwnerLabelState { Available, Missing, TooLong }
public sealed record PortfolioOwnerEvidence(PortfolioAuthorityEvidence ActorAuthority,
    PortfolioAuthorityEvidence TargetEligibility, bool SameTenant, bool Active,
    bool NamedHuman, bool Assignable, string? DisplayLabel, PortfolioOwnerLabelState LabelState);
public sealed record PortfolioOwnerCandidate(Guid UserId, string DisplayLabel);
public sealed record PortfolioOwnerCandidatesEvidence(PortfolioAuthorityEvidence Authority,
    IReadOnlyList<PortfolioOwnerCandidate> Candidates);
public sealed record PortfolioPageAccess(bool CanRead, bool CanCreate);
public sealed record PortfolioActions(bool CanEdit, bool CanAssignOwner);
public sealed record PortfolioOwnerSummary(Guid AssignmentId, string DisplayLabel);
public sealed record PortfolioOwnerHistoryItem(Guid AssignmentId, string DisplayLabel, string Reason,
    DateTime OccurredAtUtc, Guid? PreviousAssignmentId, PortfolioOwnerOperation Operation);
public sealed record PortfolioOwnerReceipt(Guid AssignmentId, Guid RequestId, int Version);

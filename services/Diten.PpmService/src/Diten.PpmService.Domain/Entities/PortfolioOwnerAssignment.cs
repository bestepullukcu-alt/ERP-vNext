namespace Diten.PpmService.Domain.Entities;

// Embedded in Portfolio; this is not an independent aggregate or identity directory.
public sealed record PortfolioOwnerAssignment(
    Guid Id, Guid UserId, string DisplayLabel, Guid ActorId, string Reason,
    DateTime OccurredAtUtc, Guid? PreviousAssignmentId, Guid RequestId,
    int ExpectedVersion, int ResultVersion, PortfolioOwnerOperation Operation,
    Guid AuditIntentId, Guid CorrelationId);

public enum PortfolioOwnerOperation { Assign, Transfer }

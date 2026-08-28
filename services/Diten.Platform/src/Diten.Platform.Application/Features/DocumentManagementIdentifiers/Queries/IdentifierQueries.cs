using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementIdentifiers.Queries;

// MOD-0029-FU07 — identifier allocation ledger read query (tenant-scoped; no side effects).

public sealed record GetIdentifierAllocationsQuery(
    string? IdentifierType,
    string? AllocationStatus,
    Guid? RegisterEntryId,
    string CorrelationId) : IRequest<Response<IReadOnlyList<IdentifierAllocationModel>>>;

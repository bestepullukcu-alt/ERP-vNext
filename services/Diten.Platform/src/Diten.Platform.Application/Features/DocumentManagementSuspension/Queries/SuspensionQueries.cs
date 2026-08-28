using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementSuspension.Queries;

// MOD-0029-FU13 — suspension / retirement / temporary-instruction read queries (tenant-scoped; no side effects).

public sealed record GetSuspensionCasesQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<SuspensionCaseModel>>>;

public sealed record GetRetirementCasesQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<RetirementCaseModel>>>;

public sealed record GetTemporaryInstructionQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<TemporaryInstructionModel>>;

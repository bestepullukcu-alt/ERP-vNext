using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;

// MOD-0029-FU06 — Document Master Register read queries (tenant-scoped; no side effects).

public sealed record GetMasterRegisterListQuery(
    string? RegisterStatus,
    string? LifecycleStatus,
    string? Criticality,
    string? DocumentClass,
    Guid? OwnerCompanyId,
    string CorrelationId) : IRequest<Response<IReadOnlyList<MasterRegisterListItemModel>>>;

public sealed record GetMasterRegisterEntryByIdQuery(Guid EntryId, string CorrelationId)
    : IRequest<Response<MasterRegisterDetailModel>>;

public sealed record GetMasterRegisterSummaryQuery(string CorrelationId)
    : IRequest<Response<MasterRegisterSummaryModel>>;

using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
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

/// <summary>WP-DM-DCP005-REGISTER-IMPORT-UI-01 — every committed CSV import batch, newest first.</summary>
public sealed record GetDocumentRegisterImportHistoryQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<DocumentRegisterImportBatchDto>>>;

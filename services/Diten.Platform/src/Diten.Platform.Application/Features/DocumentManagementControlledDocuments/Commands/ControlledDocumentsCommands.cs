using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Commands;

// MOD-0029-FU01 — controlled-document / template / share commands (sealed records; handlers delegate to services).

public sealed record CreateControlledDocumentCommand(CreateControlledDocumentInput Input, string CorrelationId)
    : IRequest<Response<ControlledDocumentDetailModel>>;

public sealed record EditControlledDocumentCommand(Guid DocumentId, EditControlledDocumentInput Input, string CorrelationId)
    : IRequest<Response<ControlledDocumentDetailModel>>;

public sealed record CreateControlledDocumentVersionCommand(Guid DocumentId, FileUploadInput File, string? ChangeSummary, string CorrelationId)
    : IRequest<Response<DocumentVersionModel>>;

public sealed record ShareControlledDocumentCommand(Guid DocumentId, Guid TargetCompanyId, string? ShareMode, string CorrelationId)
    : IRequest<Response<ShareResultModel>>;

public sealed record CreateTemplateDocumentCommand(CreateTemplateInput Input, string CorrelationId)
    : IRequest<Response<TemplateDetailModel>>;

public sealed record CreateTemplateVersionCommand(Guid TemplateId, FileUploadInput File, string? ChangeSummary, string CorrelationId)
    : IRequest<Response<DocumentVersionModel>>;

public sealed record ShareTemplateCommand(Guid TemplateId, Guid TargetCompanyId, string? ShareMode, string CorrelationId)
    : IRequest<Response<ShareResultModel>>;

public sealed record UpsertFolderDocumentAccessCommand(UpsertFolderAccessInput Input, string CorrelationId)
    : IRequest<Response<FolderAccessPolicyModel>>;

public sealed record DryRunFolderShareCommand(FolderShareInput Input, string CorrelationId)
    : IRequest<Response<FolderShareResultModel>>;

public sealed record ExecuteFolderShareCommand(FolderShareInput Input, string CorrelationId)
    : IRequest<Response<FolderShareResultModel>>;

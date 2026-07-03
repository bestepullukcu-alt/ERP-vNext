using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Queries;

// MOD-0029-FU01 — controlled-document / template / folder / share queries.

public sealed record GetControlledDocumentListQuery(Guid? CollectionInstanceId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ControlledDocumentListItemModel>>>;

public sealed record GetControlledDocumentByIdQuery(Guid DocumentId, string CorrelationId)
    : IRequest<Response<ControlledDocumentDetailModel>>;

public sealed record GetControlledDocumentVersionsQuery(Guid DocumentId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<DocumentVersionModel>>>;

public sealed record GetControlledDocumentVersionByIdQuery(Guid DocumentId, Guid VersionId, string CorrelationId)
    : IRequest<Response<DocumentVersionModel>>;

public sealed record DownloadControlledDocumentVersionQuery(Guid DocumentId, Guid VersionId, string CorrelationId)
    : IRequest<Response<DocumentDownloadResult>>;

public sealed record GetTemplateListQuery(Guid? CollectionInstanceId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<TemplateListItemModel>>>;

public sealed record GetTemplateByIdQuery(Guid TemplateId, string CorrelationId)
    : IRequest<Response<TemplateDetailModel>>;

public sealed record GetTemplateVersionsQuery(Guid TemplateId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<DocumentVersionModel>>>;

public sealed record DownloadTemplateVersionQuery(Guid TemplateId, Guid VersionId, string CorrelationId)
    : IRequest<Response<DocumentDownloadResult>>;

public sealed record GetFolderDocumentsQuery(Guid CollectionInstanceId, string CorrelationId)
    : IRequest<Response<FolderDocumentsModel>>;

public sealed record GetFolderDocumentAccessQuery(Guid CollectionInstanceId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<FolderAccessPolicyModel>>>;

public sealed record GetFolderShareOperationQuery(Guid OperationId, string CorrelationId)
    : IRequest<Response<FolderShareResultModel>>;

public sealed record GetActiveDocumentationStructuresQuery(Guid CompanyId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<DocumentationStructureModel>>>;

public sealed record SearchControlledDocumentsQuery(ExplorerSearchInput Input, string CorrelationId)
    : IRequest<Response<ExplorerSearchResultModelList>>;

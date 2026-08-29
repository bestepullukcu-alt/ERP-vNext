using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Queries;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Handlers.QueryHandlers;

public sealed class GetControlledDocumentListHandler(ControlledDocumentService service)
    : IRequestHandler<GetControlledDocumentListQuery, Response<IReadOnlyList<ControlledDocumentListItemModel>>>
{
    public Task<Response<IReadOnlyList<ControlledDocumentListItemModel>>> Handle(GetControlledDocumentListQuery request, CancellationToken ct) =>
        service.ListAsync(request.CollectionInstanceId, request.IncludeNonEffective, request.CorrelationId, ct);
}

public sealed class GetControlledDocumentByIdHandler(ControlledDocumentService service)
    : IRequestHandler<GetControlledDocumentByIdQuery, Response<ControlledDocumentDetailModel>>
{
    public Task<Response<ControlledDocumentDetailModel>> Handle(GetControlledDocumentByIdQuery request, CancellationToken ct) =>
        service.GetDetailAsync(request.DocumentId, request.CorrelationId, ct);
}

public sealed class GetControlledDocumentVersionsHandler(ControlledDocumentService service)
    : IRequestHandler<GetControlledDocumentVersionsQuery, Response<IReadOnlyList<DocumentVersionModel>>>
{
    public Task<Response<IReadOnlyList<DocumentVersionModel>>> Handle(GetControlledDocumentVersionsQuery request, CancellationToken ct) =>
        service.GetVersionsAsync(request.DocumentId, request.CorrelationId, ct);
}

public sealed class GetControlledDocumentVersionByIdHandler(ControlledDocumentService service)
    : IRequestHandler<GetControlledDocumentVersionByIdQuery, Response<DocumentVersionModel>>
{
    public Task<Response<DocumentVersionModel>> Handle(GetControlledDocumentVersionByIdQuery request, CancellationToken ct) =>
        service.GetVersionAsync(request.DocumentId, request.VersionId, request.CorrelationId, ct);
}

public sealed class DownloadControlledDocumentVersionHandler(ControlledDocumentService service)
    : IRequestHandler<DownloadControlledDocumentVersionQuery, Response<DocumentDownloadResult>>
{
    public Task<Response<DocumentDownloadResult>> Handle(DownloadControlledDocumentVersionQuery request, CancellationToken ct) =>
        service.DownloadAsync(request.DocumentId, request.VersionId, request.CorrelationId, ct);
}

public sealed class GetTemplateListHandler(TemplateService service)
    : IRequestHandler<GetTemplateListQuery, Response<IReadOnlyList<TemplateListItemModel>>>
{
    public Task<Response<IReadOnlyList<TemplateListItemModel>>> Handle(GetTemplateListQuery request, CancellationToken ct) =>
        service.ListAsync(request.CollectionInstanceId, request.CorrelationId, ct);
}

public sealed class GetTemplateByIdHandler(TemplateService service)
    : IRequestHandler<GetTemplateByIdQuery, Response<TemplateDetailModel>>
{
    public Task<Response<TemplateDetailModel>> Handle(GetTemplateByIdQuery request, CancellationToken ct) =>
        service.GetDetailAsync(request.TemplateId, request.CorrelationId, ct);
}

public sealed class GetTemplateVersionsHandler(TemplateService service)
    : IRequestHandler<GetTemplateVersionsQuery, Response<IReadOnlyList<DocumentVersionModel>>>
{
    public Task<Response<IReadOnlyList<DocumentVersionModel>>> Handle(GetTemplateVersionsQuery request, CancellationToken ct) =>
        service.GetVersionsAsync(request.TemplateId, request.CorrelationId, ct);
}

public sealed class DownloadTemplateVersionHandler(TemplateService service)
    : IRequestHandler<DownloadTemplateVersionQuery, Response<DocumentDownloadResult>>
{
    public Task<Response<DocumentDownloadResult>> Handle(DownloadTemplateVersionQuery request, CancellationToken ct) =>
        service.DownloadAsync(request.TemplateId, request.VersionId, request.CorrelationId, ct);
}

public sealed class GetFolderDocumentsHandler(FolderDocumentService service)
    : IRequestHandler<GetFolderDocumentsQuery, Response<FolderDocumentsModel>>
{
    public Task<Response<FolderDocumentsModel>> Handle(GetFolderDocumentsQuery request, CancellationToken ct) =>
        service.GetFolderDocumentsAsync(request.CollectionInstanceId, request.IncludeNonEffective, request.CorrelationId, ct);
}

public sealed class GetFolderDocumentAccessHandler(FolderDocumentService service)
    : IRequestHandler<GetFolderDocumentAccessQuery, Response<IReadOnlyList<FolderAccessPolicyModel>>>
{
    public Task<Response<IReadOnlyList<FolderAccessPolicyModel>>> Handle(GetFolderDocumentAccessQuery request, CancellationToken ct) =>
        service.GetFolderAccessAsync(request.CollectionInstanceId, request.CorrelationId, ct);
}

public sealed class GetFolderShareOperationHandler(FolderShareService service)
    : IRequestHandler<GetFolderShareOperationQuery, Response<FolderShareResultModel>>
{
    public Task<Response<FolderShareResultModel>> Handle(GetFolderShareOperationQuery request, CancellationToken ct) =>
        service.GetOperationAsync(request.OperationId, request.CorrelationId, ct);
}

public sealed class GetActiveDocumentationStructuresHandler(ControlledDocumentExplorerService service)
    : IRequestHandler<GetActiveDocumentationStructuresQuery, Response<IReadOnlyList<DocumentationStructureModel>>>
{
    public Task<Response<IReadOnlyList<DocumentationStructureModel>>> Handle(GetActiveDocumentationStructuresQuery request, CancellationToken ct) =>
        service.GetActiveStructuresAsync(request.CompanyId, request.CorrelationId, ct);
}

public sealed class SearchControlledDocumentsHandler(ControlledDocumentExplorerService service)
    : IRequestHandler<SearchControlledDocumentsQuery, Response<ExplorerSearchResultModelList>>
{
    public Task<Response<ExplorerSearchResultModelList>> Handle(SearchControlledDocumentsQuery request, CancellationToken ct) =>
        service.SearchAsync(request.Input, request.CorrelationId, ct);
}

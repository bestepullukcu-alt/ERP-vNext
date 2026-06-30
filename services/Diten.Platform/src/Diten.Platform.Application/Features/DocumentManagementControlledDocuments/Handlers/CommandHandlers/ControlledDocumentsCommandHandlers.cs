using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Domain.Enums.DocumentManagement;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Handlers.CommandHandlers;

public sealed class CreateControlledDocumentHandler(ControlledDocumentService service)
    : IRequestHandler<CreateControlledDocumentCommand, Response<ControlledDocumentDetailModel>>
{
    public Task<Response<ControlledDocumentDetailModel>> Handle(CreateControlledDocumentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.CreateAsync(request.Input, request.CorrelationId, ct);
    }
}

public sealed class EditControlledDocumentHandler(ControlledDocumentService service)
    : IRequestHandler<EditControlledDocumentCommand, Response<ControlledDocumentDetailModel>>
{
    public Task<Response<ControlledDocumentDetailModel>> Handle(EditControlledDocumentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.EditMetadataAsync(request.DocumentId, request.Input, request.CorrelationId, ct);
    }
}

public sealed class CreateControlledDocumentVersionHandler(ControlledDocumentService service)
    : IRequestHandler<CreateControlledDocumentVersionCommand, Response<DocumentVersionModel>>
{
    public Task<Response<DocumentVersionModel>> Handle(CreateControlledDocumentVersionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.CreateVersionAsync(request.DocumentId, request.File, request.ChangeSummary, request.AllowUnchanged, request.CorrelationId, ct);
    }
}

public sealed class ShareControlledDocumentHandler(TemplateSharingService service)
    : IRequestHandler<ShareControlledDocumentCommand, Response<ShareResultModel>>
{
    public Task<Response<ShareResultModel>> Handle(ShareControlledDocumentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.ShareDocumentAsync(request.DocumentId, request.TargetCompanyId, ControlledDocumentWire.ParseShareMode(request.ShareMode), request.CorrelationId, ct);
    }
}

public sealed class DeleteControlledDocumentHandler(ControlledDocumentService service)
    : IRequestHandler<DeleteControlledDocumentCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(DeleteControlledDocumentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.DeleteAsync(request.DocumentId, request.CorrelationId, ct);
    }
}

public sealed class MoveControlledDocumentHandler(ControlledDocumentService service)
    : IRequestHandler<MoveControlledDocumentCommand, Response<ControlledDocumentDetailModel>>
{
    public Task<Response<ControlledDocumentDetailModel>> Handle(MoveControlledDocumentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.MoveAsync(request.DocumentId, request.TargetCollectionInstanceId, request.CorrelationId, ct);
    }
}

public sealed class ToggleControlledDocumentFavoriteHandler(ControlledDocumentService service)
    : IRequestHandler<ToggleControlledDocumentFavoriteCommand, Response<DocumentFavoriteResult>>
{
    public Task<Response<DocumentFavoriteResult>> Handle(ToggleControlledDocumentFavoriteCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.ToggleFavoriteAsync(request.DocumentId, request.CorrelationId, ct);
    }
}

public sealed class CopyControlledDocumentHandler(ControlledDocumentService service)
    : IRequestHandler<CopyControlledDocumentCommand, Response<ControlledDocumentDetailModel>>
{
    public Task<Response<ControlledDocumentDetailModel>> Handle(CopyControlledDocumentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.CopyAsync(request.DocumentId, request.TargetCollectionInstanceId, request.TitleOverride, request.CorrelationId, ct);
    }
}

public sealed class CopyTemplateHandler(TemplateService service)
    : IRequestHandler<CopyTemplateCommand, Response<TemplateDetailModel>>
{
    public Task<Response<TemplateDetailModel>> Handle(CopyTemplateCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.CopyAsync(request.TemplateId, request.TargetCollectionInstanceId, request.TitleOverride, request.CorrelationId, ct);
    }
}

public sealed class CreateTemplateDocumentHandler(TemplateService service)
    : IRequestHandler<CreateTemplateDocumentCommand, Response<TemplateDetailModel>>
{
    public Task<Response<TemplateDetailModel>> Handle(CreateTemplateDocumentCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.CreateAsync(request.Input, request.CorrelationId, ct);
    }
}

public sealed class CreateTemplateVersionHandler(TemplateService service)
    : IRequestHandler<CreateTemplateVersionCommand, Response<DocumentVersionModel>>
{
    public Task<Response<DocumentVersionModel>> Handle(CreateTemplateVersionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.CreateVersionAsync(request.TemplateId, request.File, request.ChangeSummary, request.AllowUnchanged, request.CorrelationId, ct);
    }
}

public sealed class ShareTemplateHandler(TemplateSharingService service)
    : IRequestHandler<ShareTemplateCommand, Response<ShareResultModel>>
{
    public Task<Response<ShareResultModel>> Handle(ShareTemplateCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.ShareTemplateAsync(request.TemplateId, request.TargetCompanyId, ControlledDocumentWire.ParseShareMode(request.ShareMode), request.CorrelationId, ct);
    }
}

public sealed class UpsertFolderDocumentAccessHandler(FolderDocumentService service)
    : IRequestHandler<UpsertFolderDocumentAccessCommand, Response<FolderAccessPolicyModel>>
{
    public Task<Response<FolderAccessPolicyModel>> Handle(UpsertFolderDocumentAccessCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.UpsertFolderAccessAsync(request.Input, request.CorrelationId, ct);
    }
}

public sealed class DryRunFolderShareHandler(FolderShareService service)
    : IRequestHandler<DryRunFolderShareCommand, Response<FolderShareResultModel>>
{
    public Task<Response<FolderShareResultModel>> Handle(DryRunFolderShareCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.DryRunAsync(request.Input, request.CorrelationId, ct);
    }
}

public sealed class ExecuteFolderShareHandler(FolderShareService service)
    : IRequestHandler<ExecuteFolderShareCommand, Response<FolderShareResultModel>>
{
    public Task<Response<FolderShareResultModel>> Handle(ExecuteFolderShareCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.ExecuteAsync(request.Input, request.CorrelationId, ct);
    }
}

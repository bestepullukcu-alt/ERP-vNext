using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.QueryHandlers;

public sealed class GetCollectionInstancesHandler
    : IRequestHandler<GetCollectionInstancesQuery, Response<IReadOnlyList<CollectionInstanceListItemModel>>>
{
    private readonly ICollectionInstanceRepository _instanceRepository;
    private readonly DocumentAccessEvaluator _access;

    public GetCollectionInstancesHandler(ICollectionInstanceRepository instanceRepository, DocumentAccessEvaluator access)
    {
        _instanceRepository = instanceRepository;
        _access = access;
    }

    public async Task<Response<IReadOnlyList<CollectionInstanceListItemModel>>> Handle(GetCollectionInstancesQuery request, CancellationToken ct)
    {
        IReadOnlyList<Domain.Entities.DocumentManagement.CollectionInstance> instances;
        if (request.CompanyId.HasValue && request.BaselineReleaseId.HasValue)
        {
            instances = await _instanceRepository.GetByBaselineAndCompanyAsync(
                request.BaselineReleaseId.Value,
                request.CompanyId.Value,
                request.InstanceToken,
                ct);
        }
        else if (request.CompanyId.HasValue)
        {
            instances = await _instanceRepository.GetByCompanyAsync(request.CompanyId.Value, ct);
        }
        else
        {
            instances = await _instanceRepository.GetAllForTenantAsync(ct);
        }

        var requiredAction = ParseRequiredAction(request.RequiredAction);
        var visible = new List<Domain.Entities.DocumentManagement.CollectionInstance>();
        foreach (var instance in instances)
        {
            var allowed = requiredAction is null
                ? await _access.CanListFolderAsync(instance.Id, instance.CompanyId, ct)
                : await HasRequiredActionAsync(instance.Id, instance.CompanyId, requiredAction.Value, ct);

            if (allowed)
            {
                visible.Add(instance);
            }
        }

        // Return in the baseline's curated order (DisplayOrder, then path) so the instance tree matches the
        // Definition Tree on the Baseline Detail screen instead of falling back to a raw alphabetical/storage order.
        return Response<IReadOnlyList<CollectionInstanceListItemModel>>.Success(
            visible
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.FullPath, StringComparer.Ordinal)
                .Select(InstantiationMapping.ToListItem)
                .ToList(),
            correlationId: request.CorrelationId);
    }

    private Task<bool> HasRequiredActionAsync(
        Guid collectionInstanceId,
        Guid companyId,
        DocumentAccessMatrixAction requiredAction,
        CancellationToken ct) =>
        requiredAction switch
        {
            DocumentAccessMatrixAction.CreateDocument => _access.HasFolderCreateDocumentAsync(collectionInstanceId, ct),
            DocumentAccessMatrixAction.CreateTemplate => _access.HasFolderCreateTemplateAsync(collectionInstanceId, ct),
            DocumentAccessMatrixAction.ManageAccess => _access.HasFolderActionAsync(collectionInstanceId, DocumentAccessAction.ManageAccess, ct),
            DocumentAccessMatrixAction.Share => _access.HasFolderActionAsync(collectionInstanceId, DocumentAccessAction.Share, ct),
            DocumentAccessMatrixAction.EditMetadata => _access.HasFolderActionAsync(collectionInstanceId, DocumentAccessAction.Edit, ct),
            DocumentAccessMatrixAction.UploadVersion => _access.HasFolderActionAsync(collectionInstanceId, DocumentAccessAction.Version, ct),
            DocumentAccessMatrixAction.Download => _access.HasFolderActionAsync(collectionInstanceId, DocumentAccessAction.Download, ct),
            _ => _access.CanViewFolderAsync(collectionInstanceId, companyId, ct)
        };

    private static DocumentAccessMatrixAction? ParseRequiredAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        return Enum.TryParse<DocumentAccessMatrixAction>(action.Trim(), true, out var parsed)
            ? parsed
            : DocumentAccessMatrixAction.View;
    }
}

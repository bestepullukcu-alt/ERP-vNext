using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.QueryHandlers;

public sealed class GetDocumentAccessPolicyListHandler(DocumentAccessMatrixService service)
    : IRequestHandler<GetDocumentAccessPolicyListQuery, Response<IReadOnlyList<DocumentAccessPolicyListItemModel>>>
{
    public Task<Response<IReadOnlyList<DocumentAccessPolicyListItemModel>>> Handle(GetDocumentAccessPolicyListQuery request, CancellationToken ct) =>
        service.ListAsync(request.Filter, request.CorrelationId, ct);
}

using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.QueryHandlers;

public sealed class GetDocumentAccessPolicyByIdHandler(DocumentAccessMatrixService service)
    : IRequestHandler<GetDocumentAccessPolicyByIdQuery, Response<DocumentAccessPolicyDetailModel>>
{
    public Task<Response<DocumentAccessPolicyDetailModel>> Handle(GetDocumentAccessPolicyByIdQuery request, CancellationToken ct) =>
        service.GetDetailAsync(request.Id, request.CorrelationId, ct);
}

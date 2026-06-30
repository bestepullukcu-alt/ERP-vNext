using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.QueryHandlers;

public sealed class GetEffectiveDocumentAccessHandler(DocumentAccessMatrixService service)
    : IRequestHandler<GetEffectiveDocumentAccessQuery, Response<EffectiveDocumentAccessModel>>
{
    public Task<Response<EffectiveDocumentAccessModel>> Handle(GetEffectiveDocumentAccessQuery request, CancellationToken ct) =>
        service.GetEffectiveAsync(request.TargetType, request.TargetId, request.PrincipalType, request.PrincipalId, request.CorrelationId, ct);
}

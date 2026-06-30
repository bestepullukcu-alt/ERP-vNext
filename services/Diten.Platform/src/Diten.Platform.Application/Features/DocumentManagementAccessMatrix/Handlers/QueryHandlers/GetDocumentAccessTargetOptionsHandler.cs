using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.QueryHandlers;

public sealed class GetDocumentAccessTargetOptionsHandler(DocumentAccessMatrixService service)
    : IRequestHandler<GetDocumentAccessTargetOptionsQuery, Response<IReadOnlyList<DocumentAccessPolicyTargetModel>>>
{
    public Task<Response<IReadOnlyList<DocumentAccessPolicyTargetModel>>> Handle(GetDocumentAccessTargetOptionsQuery request, CancellationToken ct) =>
        service.GetTargetOptionsAsync(request.CorrelationId, ct);
}

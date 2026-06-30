using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.QueryHandlers;

public sealed class GetDocumentAccessPrincipalOptionsHandler(DocumentAccessMatrixService service)
    : IRequestHandler<GetDocumentAccessPrincipalOptionsQuery, Response<IReadOnlyList<DocumentAccessPrincipalModel>>>
{
    public Task<Response<IReadOnlyList<DocumentAccessPrincipalModel>>> Handle(GetDocumentAccessPrincipalOptionsQuery request, CancellationToken ct) =>
        service.GetPrincipalOptionsAsync(request.CorrelationId, ct);
}

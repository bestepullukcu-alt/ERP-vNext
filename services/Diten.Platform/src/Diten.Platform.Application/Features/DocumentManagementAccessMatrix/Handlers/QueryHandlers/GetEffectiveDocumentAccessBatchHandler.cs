using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.QueryHandlers;

public sealed class GetEffectiveDocumentAccessBatchHandler(DocumentAccessMatrixService service)
    : IRequestHandler<GetEffectiveDocumentAccessBatchQuery, Response<IReadOnlyList<EffectiveDocumentAccessModel>>>
{
    public Task<Response<IReadOnlyList<EffectiveDocumentAccessModel>>> Handle(GetEffectiveDocumentAccessBatchQuery request, CancellationToken ct) =>
        service.GetEffectiveBatchAsync(request.Input, request.CorrelationId, ct);
}

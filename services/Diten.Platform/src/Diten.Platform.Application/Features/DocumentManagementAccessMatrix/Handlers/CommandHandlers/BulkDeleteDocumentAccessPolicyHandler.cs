using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Commands;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.CommandHandlers;

public sealed class BulkDeleteDocumentAccessPolicyHandler(DocumentAccessMatrixService service)
    : IRequestHandler<BulkDeleteDocumentAccessPolicyCommand, Response<int>>
{
    public Task<Response<int>> Handle(BulkDeleteDocumentAccessPolicyCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.BulkDeleteAsync(request.Ids, request.CorrelationId, ct);
    }
}

using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Commands;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.CommandHandlers;

public sealed class DeleteDocumentAccessPolicyHandler(DocumentAccessMatrixService service)
    : IRequestHandler<DeleteDocumentAccessPolicyCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(DeleteDocumentAccessPolicyCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.DeleteAsync(request.Id, request.CorrelationId, ct);
    }
}

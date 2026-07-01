using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Commands;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.CommandHandlers;

public sealed class UpdateDocumentAccessPolicyHandler(DocumentAccessMatrixService service)
    : IRequestHandler<UpdateDocumentAccessPolicyCommand, Response<DocumentAccessPolicyDetailModel>>
{
    public Task<Response<DocumentAccessPolicyDetailModel>> Handle(UpdateDocumentAccessPolicyCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.UpdateAsync(request.Id, request.Input, request.CorrelationId, ct);
    }
}

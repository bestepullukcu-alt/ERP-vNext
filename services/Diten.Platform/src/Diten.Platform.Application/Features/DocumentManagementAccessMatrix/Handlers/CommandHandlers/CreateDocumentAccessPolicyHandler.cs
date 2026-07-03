using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Commands;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Handlers.CommandHandlers;

public sealed class CreateDocumentAccessPolicyHandler(DocumentAccessMatrixService service)
    : IRequestHandler<CreateDocumentAccessPolicyCommand, Response<DocumentAccessPolicyDetailModel>>
{
    public Task<Response<DocumentAccessPolicyDetailModel>> Handle(CreateDocumentAccessPolicyCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.CreateAsync(request.Input, request.CorrelationId, ct);
    }
}

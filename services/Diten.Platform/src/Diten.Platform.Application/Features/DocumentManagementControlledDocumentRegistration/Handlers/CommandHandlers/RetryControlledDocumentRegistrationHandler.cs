using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Handlers.CommandHandlers;

public sealed class RetryControlledDocumentRegistrationHandler(ControlledDocumentRegistrationService service)
    : IRequestHandler<RetryControlledDocumentRegistrationCommand, Response<RetryControlledDocumentRegistrationResultModel>>
{
    public Task<Response<RetryControlledDocumentRegistrationResultModel>> Handle(
        RetryControlledDocumentRegistrationCommand request, CancellationToken ct) =>
        service.RetryAsync(request.OperationId, request.CorrelationId, ct);
}

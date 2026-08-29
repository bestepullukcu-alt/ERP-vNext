using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Handlers.CommandHandlers;

public sealed class CreateControlledDocumentRegistrationHandler(ControlledDocumentRegistrationService service)
    : IRequestHandler<CreateControlledDocumentRegistrationCommand, Response<ControlledDocumentRegistrationResultModel>>
{
    public Task<Response<ControlledDocumentRegistrationResultModel>> Handle(
        CreateControlledDocumentRegistrationCommand request, CancellationToken ct) =>
        service.CreateAsync(request.Input, request.CorrelationId, ct);
}

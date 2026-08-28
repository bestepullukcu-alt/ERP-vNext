using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Queries;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Handlers.QueryHandlers;

public sealed class GetControlledDocumentRegistrationOperationHandler(ControlledDocumentRegistrationService service)
    : IRequestHandler<GetControlledDocumentRegistrationOperationQuery, Response<ControlledDocumentRegistrationOperationModel>>
{
    public Task<Response<ControlledDocumentRegistrationOperationModel>> Handle(
        GetControlledDocumentRegistrationOperationQuery request, CancellationToken ct) =>
        service.GetOperationAsync(request.OperationId, request.CorrelationId, ct);
}

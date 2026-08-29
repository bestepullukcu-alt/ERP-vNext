using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Queries;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Handlers.QueryHandlers;

public sealed class GetMasterRegisterByControlledDocumentHandler(ControlledDocumentRegistrationService service)
    : IRequestHandler<GetMasterRegisterByControlledDocumentQuery, Response<MasterRegisterByControlledDocumentModel>>
{
    public Task<Response<MasterRegisterByControlledDocumentModel>> Handle(
        GetMasterRegisterByControlledDocumentQuery request, CancellationToken ct) =>
        service.GetMasterRegisterAsync(request.ControlledDocumentId, request.CorrelationId, ct);
}

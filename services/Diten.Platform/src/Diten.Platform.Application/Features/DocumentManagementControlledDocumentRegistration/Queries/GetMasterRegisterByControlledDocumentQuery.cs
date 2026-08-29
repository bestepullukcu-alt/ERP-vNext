using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Queries;

public sealed record GetMasterRegisterByControlledDocumentQuery(Guid ControlledDocumentId, string CorrelationId)
    : IRequest<Response<MasterRegisterByControlledDocumentModel>>;

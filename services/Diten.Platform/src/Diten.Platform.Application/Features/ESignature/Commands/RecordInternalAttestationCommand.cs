using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Commands;

public sealed record RecordInternalAttestationCommand(
    Guid TargetTenantId,
    Guid EnvelopeId,
    RecordInternalAttestationRequest Request) : IRequest<Response<Guid>>;

using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Commands;

public sealed record CreateSignatureEnvelopeCommand(
    Guid TargetTenantId,
    CreateSignatureEnvelopeRequest Request) : IRequest<Response<Guid>>;

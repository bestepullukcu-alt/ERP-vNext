using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Commands;

public sealed record CancelSignatureEnvelopeCommand(
    Guid TargetTenantId,
    Guid EnvelopeId,
    CancelSignatureEnvelopeRequest Request) : IRequest<Response<NoContent>>;

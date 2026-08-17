using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Queries;

public sealed record GetSignatureEnvelopeByIdQuery(
    Guid TargetTenantId,
    Guid EnvelopeId) : IRequest<Response<SignatureEnvelopeDetailDto>>;

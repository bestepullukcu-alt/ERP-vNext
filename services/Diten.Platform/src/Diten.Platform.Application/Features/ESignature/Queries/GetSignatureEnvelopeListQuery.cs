using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Queries;

public sealed record GetSignatureEnvelopeListQuery(
    Guid TargetTenantId,
    int Page = 1,
    int PageSize = 25) : IRequest<Response<SignatureEnvelopePageDto>>;

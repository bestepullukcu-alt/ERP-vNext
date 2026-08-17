using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Queries;

public sealed record GetSignatureArtifactQuery(
    Guid TargetTenantId,
    Guid ArtifactId) : IRequest<Response<SignatureArtifactDto>>;

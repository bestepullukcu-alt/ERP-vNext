using System.Security.Cryptography;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ESignature.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers.QueryHandlers;

public sealed class GetSignatureArtifactHandler
    : IRequestHandler<GetSignatureArtifactQuery, Response<SignatureArtifactDto>>
{
    private readonly IESignatureRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSignatureArtifactHandler(IESignatureRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SignatureArtifactDto>> Handle(GetSignatureArtifactQuery query, CancellationToken ct)
    {
        using var tenantScope = TenantScope.BeginPlatform(_tenantContext, query.TargetTenantId);
        var artifact = await _repository.GetArtifactAsync(query.ArtifactId, ct);
        if (artifact is null)
        {
            return Response<SignatureArtifactDto>.Fail("Signature artifact was not found.", 404);
        }

        var computedHash = Convert.ToHexString(SHA256.HashData(artifact.Content)).ToLowerInvariant();
        if (!string.Equals(computedHash, artifact.Sha256, StringComparison.Ordinal))
        {
            return Response<SignatureArtifactDto>.Fail(
                "Signature artifact integrity verification failed.",
                409);
        }

        return Response<SignatureArtifactDto>.Success(
            new SignatureArtifactDto(
                artifact.Id,
                artifact.FileName,
                artifact.ContentType,
                artifact.Content,
                artifact.Sha256));
    }
}

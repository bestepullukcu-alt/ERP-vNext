using System.Security.Cryptography;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Infrastructure.Services.ESignature;

public sealed class InternalSignatureArtifactStore : IInternalSignatureArtifactStore
{
    private readonly IESignatureRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public InternalSignatureArtifactStore(
        IESignatureRepository repository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<StoredSignatureArtifact> StoreAsync(
        Guid envelopeId,
        string artifactType,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken ct = default)
    {
        if (content.Length == 0)
        {
            throw new InvalidOperationException("Signature artifact content is required.");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var artifact = new InternalSignatureArtifact
        {
            TenantId = _tenantContext.TenantId,
            EnvelopeId = envelopeId,
            ArtifactType = artifactType,
            FileName = fileName,
            ContentType = contentType,
            Content = content,
            Sha256 = sha256,
            RetainUntil = DateTimeOffset.UtcNow.AddYears(7),
            CreatedBy = _currentUser.ActorName
        };
        await _repository.AddArtifactAsync(artifact, ct);
        return new StoredSignatureArtifact(artifact.Id, sha256);
    }
}

namespace Diten.Platform.Application.Features.ESignature.Services;

public interface IInternalSignatureArtifactStore
{
    Task<StoredSignatureArtifact> StoreAsync(
        Guid envelopeId,
        string artifactType,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken ct = default);
}

public sealed record StoredSignatureArtifact(Guid Id, string Sha256);

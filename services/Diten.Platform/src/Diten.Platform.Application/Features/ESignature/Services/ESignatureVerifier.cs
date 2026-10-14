using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.ESignature.Services;

/// <summary>
/// MOD-0022 — verification (pack §8 "Verification", ESIGN-BUNDLE §2.6). Re-resolves the subject and recomputes its hash,
/// reloads the PNG and recomputes its hash, recomputes the signature digest, and reads the invalidation. NOTHING is written
/// back: unlike FU23 there is no RequiresResign status — the verdict is computed every time.
/// </summary>
public interface IESignatureVerifier
{
    Task<VerificationResultDto> VerifyAsync(ESignatureRecord record, ESignatureInvalidation? invalidation, CancellationToken ct);
}

public sealed class ESignatureVerifier : IESignatureVerifier
{
    private readonly ISignableSubjectRegistry _subjects;
    private readonly IContentStorageGateway _storage;

    public ESignatureVerifier(ISignableSubjectRegistry subjects, IContentStorageGateway storage)
    {
        _subjects = subjects;
        _storage = storage;
    }

    public async Task<VerificationResultDto> VerifyAsync(ESignatureRecord record, ESignatureInvalidation? invalidation, CancellationToken ct)
    {
        var resolution = await _subjects.ResolveAsync(record.Module, record.SubjectType, record.SubjectId, ct);
        string? currentHash = null;
        if (resolution.Outcome == SignableSubjectOutcome.Found)
        {
            currentHash = ESignatureDigest.SubjectHash(record.Module, record.SubjectType, record.SubjectId, resolution.Subject!.Fields);
        }

        var imageIntact = await ImageIntactAsync(record, ct);
        var digestIntact = string.Equals(ESignatureDigest.SignatureDigest(record.DigestInput()), record.SignatureDigest, StringComparison.Ordinal);
        var status = ESignatureRules.Verdict(
            invalidated: invalidation is not null,
            imageIntact: imageIntact,
            digestIntact: digestIntact,
            subjectResolved: currentHash is not null,
            subjectUnchanged: string.Equals(currentHash, record.SubjectHash, StringComparison.Ordinal));

        return new VerificationResultDto(record.Id, ESignatureWire.Name(status), record.SubjectHash, currentHash, imageIntact, digestIntact, DateTime.UtcNow);
    }

    /// <summary>No image ⇒ nothing to tamper with. An image that can no longer be read counts as tampered (evidence lost).</summary>
    private async Task<bool> ImageIntactAsync(ESignatureRecord record, CancellationToken ct)
    {
        if (record.AppearanceImageHash is null)
        {
            return record.AppearanceStoragePath is null;
        }

        if (string.IsNullOrWhiteSpace(record.AppearanceStoragePath) || string.IsNullOrWhiteSpace(record.AppearanceStorageProvider))
        {
            return false;
        }

        var opened = await _storage.OpenReadAsync(record.AppearanceStorageProvider, record.AppearanceStoragePath, ct);
        if (!opened.IsSuccessful || opened.Data is null)
        {
            return false;
        }

        await using var stream = opened.Data.Content;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return string.Equals(ESignatureDigest.ImageHash(buffer.ToArray()), record.AppearanceImageHash, StringComparison.Ordinal);
    }
}

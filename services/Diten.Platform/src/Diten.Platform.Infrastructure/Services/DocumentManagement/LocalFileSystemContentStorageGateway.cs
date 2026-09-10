using System.Security.Cryptography;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.DocumentManagement;

/// <summary>
/// MOD-0262-FU01 — local-filesystem <see cref="IContentStorageGateway"/> provider. Files live under a
/// config-driven root that MUST NOT be under wwwroot and is never exposed by any public/static URL; access is
/// always through a backend API after the calling application service has authorised (AD-5). The object key is
/// built deterministically and always carries the tenant, so tenant isolation is structural rather than a query
/// filter. File names are sanitized; allowed extensions/media types and the maximum size are enforced; a
/// SHA-256 checksum is computed. Raw bytes are never written to Mongo and no physical business folder is
/// created (only a storage object path).
/// <para>
/// <b>AD-4 — streaming.</b> The payload arrives as a forward-only <see cref="Stream"/>. Content is copied to
/// disk in bounded chunks while the checksum is computed incrementally, so an upload is never materialised
/// whole in memory. The size limit is therefore enforced <i>during</i> the copy: when it is exceeded the
/// partial object is deleted before failing, so an over-size upload leaves nothing behind.
/// </para>
/// <para>
/// ⛔ <b>AD-6.</b> <see cref="TryDeleteAsync"/> is best-effort orphan compensation only. It is not a purge path
/// and must not be reused as one; physical destruction under a disposition decision is MOD-0262-FU05.
/// </para>
/// </summary>
public sealed class LocalFileSystemContentStorageGateway : IContentStorageGateway
{
    public const string ProviderName = "local-filesystem";

    private const int CopyBufferSize = 81_920;

    private readonly ContentStorageOptions _options;
    private readonly ILogger<LocalFileSystemContentStorageGateway> _logger;
    private readonly string _root;

    public LocalFileSystemContentStorageGateway(
        IOptions<ContentStorageOptions> options,
        ILogger<LocalFileSystemContentStorageGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
        _root = ResolveRoot(_options.RootPath);
    }

    public async Task<Response<ContentStoreResult>> StoreAsync(ContentStoreRequest request, CancellationToken ct = default)
    {
        var safeFileName = SanitizeFileName(request.FileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();

        if (_options.AllowedExtensions.Count > 0 && !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return Response<ContentStoreResult>.Fail($"File type '{extension}' is not allowed.", 400, DocumentRepositoryReasonCodes.ValidationFailed);
        }

        var mediaType = string.IsNullOrWhiteSpace(request.DeclaredMediaType) ? "application/octet-stream" : request.DeclaredMediaType.Trim();
        if (_options.AllowedMediaTypes.Count > 0 && !_options.AllowedMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
        {
            return Response<ContentStoreResult>.Fail($"Media type '{mediaType}' is not allowed.", 400, DocumentRepositoryReasonCodes.ValidationFailed);
        }

        if (request.Content is null)
        {
            return Response<ContentStoreResult>.Fail("Uploaded file is empty.", 400, DocumentRepositoryReasonCodes.ValidationFailed);
        }

        string objectKey;
        try
        {
            objectKey = BuildObjectKey(request, safeFileName);
        }
        catch (InvalidOperationException)
        {
            return Response<ContentStoreResult>.Fail("Storage partition is invalid.", 400, DocumentRepositoryReasonCodes.ValidationFailed);
        }

        var absolutePath = Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar));
        long byteSize;
        string checksum;

        try
        {
            var directory = Path.GetDirectoryName(absolutePath)!;
            Directory.CreateDirectory(directory);

            var (size, hash, overflow) = await WriteStreamAsync(request.Content, absolutePath, _options.MaxFileSizeBytes, ct);

            if (overflow)
            {
                TryRemovePartial(absolutePath);
                return Response<ContentStoreResult>.Fail("File exceeds the maximum allowed size.", 400, DocumentRepositoryReasonCodes.ValidationFailed);
            }

            if (size == 0)
            {
                TryRemovePartial(absolutePath);
                return Response<ContentStoreResult>.Fail("Uploaded file is empty.", 400, DocumentRepositoryReasonCodes.ValidationFailed);
            }

            byteSize = size;
            checksum = hash;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            TryRemovePartial(absolutePath);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryRemovePartial(absolutePath);
            _logger.LogError(ex, "Content storage write failed for object {ObjectKey}.", objectKey);
            return Response<ContentStoreResult>.Fail("Content storage is unavailable.", 503, DocumentRepositoryReasonCodes.StorageUnavailable);
        }

        return Response<ContentStoreResult>.Success(new ContentStoreResult(
            Guid.NewGuid(), ProviderName, objectKey, safeFileName, mediaType, byteSize, checksum));
    }

    public Task<Response<ContentStreamResult>> OpenReadAsync(string storageProvider, string objectKey, CancellationToken ct = default)
    {
        if (!IsSafeObjectKey(objectKey))
        {
            return Task.FromResult(Response<ContentStreamResult>.Fail("Content not found.", 404, DocumentRepositoryReasonCodes.NotFoundNonLeakage));
        }

        var absolutePath = Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolutePath))
        {
            return Task.FromResult(Response<ContentStreamResult>.Fail("Content not found.", 404, DocumentRepositoryReasonCodes.NotFoundNonLeakage));
        }

        try
        {
            Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);
            var fileName = Path.GetFileName(absolutePath);
            return Task.FromResult(Response<ContentStreamResult>.Success(
                new ContentStreamResult(stream, "application/octet-stream", fileName, stream.Length)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Content storage read failed for object {ObjectKey}.", objectKey);
            return Task.FromResult(Response<ContentStreamResult>.Fail("Content storage is unavailable.", 503, DocumentRepositoryReasonCodes.StorageUnavailable));
        }
    }

    /// <summary>⛔ Best-effort orphan compensation ONLY (AD-6). Never a purge path.</summary>
    public Task<bool> TryDeleteAsync(string storageProvider, string objectKey, CancellationToken ct = default)
    {
        try
        {
            if (!IsSafeObjectKey(objectKey))
            {
                return Task.FromResult(false);
            }

            var absolutePath = Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Best-effort delete failed for object {ObjectKey}; orphan-cleanup follow-up required.", objectKey);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Copies the upload to disk in bounded chunks while hashing incrementally. Returns the byte count, the
    /// lowercase-hex SHA-256, and whether the configured maximum was exceeded (in which case the caller removes
    /// the partial object). Never buffers the whole payload.
    /// </summary>
    private static async Task<(long Size, string Checksum, bool Overflow)> WriteStreamAsync(
        Stream source, string absolutePath, long maxBytes, CancellationToken ct)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[CopyBufferSize];
        long total = 0;

        await using (var destination = new FileStream(
            absolutePath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true))
        {
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                total += read;
                if (maxBytes > 0 && total > maxBytes)
                {
                    return (total, string.Empty, true);
                }

                hasher.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            }

            await destination.FlushAsync(ct);
        }

        return (total, Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant(), false);
    }

    private static void TryRemovePartial(string absolutePath)
    {
        try
        {
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }
        catch
        {
            // Compensation is best effort by design; the caller already reports the primary failure.
        }
    }

    private static string BuildObjectKey(ContentStoreRequest request, string safeFileName)
    {
        var scope = request.Scope == ContentStorageScope.Templates ? "templates" : "documents";
        if (!string.IsNullOrWhiteSpace(request.StoragePartition))
        {
            var partition = request.StoragePartition.Trim().Replace('\\', '/').Trim('/');
            if (partition.Contains("..", StringComparison.Ordinal)
                || !partition.StartsWith($"tenant/{request.TenantId:D}/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Storage partition is invalid.");
            }
            return $"{partition}/{scope}/{request.ItemId:D}/versions/{request.VersionId:D}/{safeFileName}";
        }
        return $"tenant-{request.TenantId:D}/company-{request.CompanyId:D}/{scope}/{request.ItemId:D}/versions/{request.VersionId:D}/{safeFileName}";
    }

    /// <summary>Rejects traversal and absolute paths before an object key ever reaches the filesystem.</summary>
    private static bool IsSafeObjectKey(string? objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return false;
        }

        var normalized = objectKey.Replace('\\', '/');
        return !normalized.Contains("..", StringComparison.Ordinal)
            && !normalized.StartsWith('/')
            && !Path.IsPathRooted(normalized);
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "file";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        cleaned = cleaned.Replace("..", "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned;
    }

    private static string ResolveRoot(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        // Safe default for dev/local: under the OS temp dir, never under wwwroot, never a public URL.
        return Path.Combine(Path.GetTempPath(), "DitenStorage", "Documents");
    }
}

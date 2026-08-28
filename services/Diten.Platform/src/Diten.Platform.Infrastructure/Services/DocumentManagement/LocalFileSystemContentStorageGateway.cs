using System.Security.Cryptography;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — Phase 1 <see cref="IContentStorageGateway"/> implementation. Files live on the server under a
/// config-driven root that MUST NOT be under wwwroot and is never exposed by any public/static URL; access is
/// always through a backend API after permission checks. The object key is built deterministically per
/// tenant/company/item/version; file names are sanitized; allowed extensions/media types and max size are
/// enforced; a SHA-256 checksum is computed. Raw bytes are never written to Mongo and no physical business
/// folder is created (only a storage object path). This is the only place FU01 touches the filesystem, and it
/// does so behind the seam — never as inline controller/handler code.
/// </summary>
public sealed class LocalFileSystemContentStorageGateway : IContentStorageGateway
{
    public const string ProviderName = "local-filesystem";

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
            return Response<ContentStoreResult>.Fail($"File type '{extension}' is not allowed.", 400, ControlledDocumentReasonCodes.ValidationFailed);
        }

        var mediaType = string.IsNullOrWhiteSpace(request.DeclaredMediaType) ? "application/octet-stream" : request.DeclaredMediaType.Trim();
        if (_options.AllowedMediaTypes.Count > 0 && !_options.AllowedMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
        {
            return Response<ContentStoreResult>.Fail($"Media type '{mediaType}' is not allowed.", 400, ControlledDocumentReasonCodes.ValidationFailed);
        }

        if (request.Content.LongLength == 0)
        {
            return Response<ContentStoreResult>.Fail("Uploaded file is empty.", 400, ControlledDocumentReasonCodes.ValidationFailed);
        }

        if (request.Content.LongLength > _options.MaxFileSizeBytes)
        {
            return Response<ContentStoreResult>.Fail("File exceeds the maximum allowed size.", 400, ControlledDocumentReasonCodes.ValidationFailed);
        }

        var objectKey = BuildObjectKey(request, safeFileName);
        var checksum = ComputeChecksum(request.Content);

        try
        {
            var absolutePath = Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(absolutePath)!;
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(absolutePath, request.Content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Content storage write failed for object {ObjectKey}.", objectKey);
            return Response<ContentStoreResult>.Fail("Content storage is unavailable.", 503, ControlledDocumentReasonCodes.StorageUnavailable);
        }

        return Response<ContentStoreResult>.Success(new ContentStoreResult(
            Guid.NewGuid(), ProviderName, objectKey, safeFileName, mediaType, request.Content.LongLength, checksum));
    }

    public Task<Response<ContentStreamResult>> OpenReadAsync(string storageProvider, string objectKey, CancellationToken ct = default)
    {
        var absolutePath = Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolutePath))
        {
            return Task.FromResult(Response<ContentStreamResult>.Fail("Content not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage));
        }

        try
        {
            Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileName = Path.GetFileName(absolutePath);
            return Task.FromResult(Response<ContentStreamResult>.Success(
                new ContentStreamResult(stream, "application/octet-stream", fileName, stream.Length)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Content storage read failed for object {ObjectKey}.", objectKey);
            return Task.FromResult(Response<ContentStreamResult>.Fail("Content storage is unavailable.", 503, ControlledDocumentReasonCodes.StorageUnavailable));
        }
    }

    public Task<bool> TryDeleteAsync(string storageProvider, string objectKey, CancellationToken ct = default)
    {
        try
        {
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

    private static string ComputeChecksum(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexString(hash).ToLowerInvariant();
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

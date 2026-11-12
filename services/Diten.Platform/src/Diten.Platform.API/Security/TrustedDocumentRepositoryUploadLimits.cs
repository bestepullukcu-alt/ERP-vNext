using Diten.Platform.Application.Contracts.DocumentRepository;
using Microsoft.AspNetCore.WebUtilities;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0262-FU01 DL-2 — the trusted consumer path's size limits (K-E, owner approval Beste 2026-11-03). Fixed in code, not
/// configured; narrower than the general, configured <c>ContentStorageOptions.MaxFileSizeBytes</c>, which stays as it is.
/// </summary>
public static class TrustedDocumentRepositoryUploadLimits
{
    /// <summary>50 MiB — the file section's limit.</summary>
    public const long MaxFileBytes = 52_428_800;

    /// <summary>The request body's limit: the file plus 1 MiB for the form fields and boundaries (53 477 376).</summary>
    public const long MaxRequestBytes = MaxFileBytes + 1_048_576;

    /// <summary>All form fields together (they come before the file section).</summary>
    public const int MaxFormFieldBytes = 16 * 1024;
}

/// <summary>
/// MOD-0262-FU01 DL-2 (r5, F-5) — the counting stream the executor wraps around the multipart file section and hands to
/// the store command. It is how an over-limit file is told apart from any other storage failure INSIDE the command:
/// <list type="bullet">
/// <item>More than <see cref="TrustedDocumentRepositoryUploadLimits.MaxFileBytes"/> bytes → <see cref="LimitExceeded"/>
/// and a <see cref="TrustedUploadLimitExceededException"/> (an <see cref="IOException"/>, so the provider removes the
/// partial file and answers 503; the handler turns that into 413).</item>
/// <item>The server's own body limit (<c>BadHttpRequestException</c> with status 413) on an inner read → the same flag,
/// the exception rethrown as it is (defence; TestServer cannot raise it).</item>
/// <item>At the end of the file section the multipart reader is asked for a next section; when there is one (a second
/// file, a field after the file) → <see cref="FormatViolated"/> and an <see cref="IOException"/> (handler → 400).</item>
/// </list>
/// </summary>
public sealed class TrustedUploadLimitedReadStream : Stream, ITrustedUploadLimitSignal
{
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private readonly Func<CancellationToken, Task<bool>>? _hasTrailingSection;
    private long _read;
    private bool _endChecked;

    public TrustedUploadLimitedReadStream(
        Stream inner,
        long maxBytes = TrustedDocumentRepositoryUploadLimits.MaxFileBytes,
        Func<CancellationToken, Task<bool>>? hasTrailingSection = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _maxBytes = maxBytes;
        _hasTrailingSection = hasTrailingSection;
    }

    /// <summary>Builds the trailing-section probe over the reader the file section came from.</summary>
    public static Func<CancellationToken, Task<bool>> TrailingSectionProbe(MultipartReader reader) =>
        async ct => await reader.ReadNextSectionAsync(ct) is not null;

    public bool LimitExceeded { get; private set; }

    public bool FormatViolated { get; private set; }

    public long BytesRead => _read;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read;
        try
        {
            read = await _inner.ReadAsync(buffer, cancellationToken);
        }
        catch (IOException ex) when (IsServerBodyLimit(ex))
        {
            LimitExceeded = true;
            throw;
        }

        if (read == 0)
        {
            await CheckEndAsync(cancellationToken);
            return 0;
        }

        _read += read;
        if (_read > _maxBytes)
        {
            LimitExceeded = true;
            throw new TrustedUploadLimitExceededException(_maxBytes);
        }

        return read;
    }

    private async Task CheckEndAsync(CancellationToken cancellationToken)
    {
        if (_endChecked || _hasTrailingSection is null)
        {
            return;
        }

        _endChecked = true;
        if (await _hasTrailingSection(cancellationToken))
        {
            FormatViolated = true;
            throw new IOException("The multipart body continues after the file section.");
        }
    }

    /// <summary>Kestrel's body-limit failure: <c>Microsoft.AspNetCore.Http.BadHttpRequestException</c> with status 413.</summary>
    private static bool IsServerBodyLimit(IOException ex) =>
        ex is Microsoft.AspNetCore.Http.BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge };

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>MOD-0262-FU01 DL-2 — the file section passed the trusted limit while it was being copied.</summary>
public sealed class TrustedUploadLimitExceededException : IOException
{
    public TrustedUploadLimitExceededException(long maxBytes)
        : base($"The uploaded file exceeds the trusted consumer limit of {maxBytes} bytes.")
    {
    }
}

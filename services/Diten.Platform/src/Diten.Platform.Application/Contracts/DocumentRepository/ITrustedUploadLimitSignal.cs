namespace Diten.Platform.Application.Contracts.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 DL-2 (r5, F-5) — the one place the Application layer reads the trusted consumer upload's counting
/// stream. The API wraps the multipart file section in a stream that implements this; the store handler reads it when
/// the provider answered a failure, so the outcome the audit record and the client see is decided INSIDE the command
/// (Application does not reference ASP.NET, so the stream type itself cannot be named here).
/// </summary>
public interface ITrustedUploadLimitSignal
{
    /// <summary>The file bytes passed <c>TrustedDocumentRepositoryUploadLimits.MaxFileBytes</c> (or the server's own
    /// body limit fired) while the provider was copying → 413.</summary>
    bool LimitExceeded { get; }

    /// <summary>
    /// DL-2 geliştirici kararı — the multipart body went on after the file section (a second file or a field after the
    /// file). Streaming cannot see that before the file is copied; it is found at the end of the file section, the copy
    /// fails, the provider removes the partial file and the handler answers 400 <c>VALIDATION_FAILED</c>.
    /// </summary>
    bool FormatViolated { get; }
}

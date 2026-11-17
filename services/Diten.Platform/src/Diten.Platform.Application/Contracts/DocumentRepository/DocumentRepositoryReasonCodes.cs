namespace Diten.Platform.Application.Contracts.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 — reason codes emitted by the document repository seam.
/// <para>
/// The string values are deliberately <b>identical</b> to the MOD-0029 codes the gateway emitted before the
/// relocation (DCP-008 AD-2: existing consumers must behave identically). Only the dependency direction
/// changed: the repository no longer reaches into a consumer's feature namespace for its own codes.
/// </para>
/// </summary>
public static class DocumentRepositoryReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string StorageUnavailable = "STORAGE_UNAVAILABLE";
    public const string Forbidden = "FORBIDDEN";

    // ── MOD-0262-FU01 DL-2 — the trusted consumer surface (api/internal/v1/document-repository/trusted-consumer, §13).
    public const string TrustedConsumerUnauthenticated = "DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAUTHENTICATED";
    public const string TrustedConsumerForbidden = "DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_FORBIDDEN";
    public const string TrustedConsumerTooLarge = "DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TOO_LARGE";
    public const string TrustedConsumerUnavailable = "DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAVAILABLE";
    public const string TrustedConsumerTimeout = "DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TIMEOUT";

    /// <summary>The client went away while the command ran: "the client got no answer", NOT "nothing happened" (§8.1).</summary>
    public const string TrustedConsumerClientAborted = "DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_CLIENT_ABORTED";
}

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
}

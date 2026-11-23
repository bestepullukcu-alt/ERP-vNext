namespace Diten.MdmService.Infrastructure.RecordCodes;

/// <summary>
/// RECORD-CODE-ALLOCATE 1.0.0 §1 — how MdmService reaches the shared record code service (Platform). Every value comes from
/// the environment's configuration (<c>RecordCodeAllocator__PlatformBaseAddress</c> …); none is in a tracked settings file and
/// no secret is written anywhere in the repository. A value that is missing means the port is NOT BOUND: the adapter answers
/// "unavailable" and a record that needs a code is not created (fail closed, KARAR D59/1).
/// </summary>
public sealed class RecordCodeAllocatorOptions
{
    public const string SectionName = "RecordCodeAllocator";

    /// <summary>Platform's base address (https, or http on a loopback address only).</summary>
    public Uri? PlatformBaseAddress { get; set; }

    /// <summary>Platform's own budget for an allocation is 5 s (§4 RECORD_CODE_UNAVAILABLE); MDM waits a little longer.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(6);

    /// <summary>The credential id of the <c>Mdm</c> binding (<c>Diten.MdmService</c>; allowed type <c>BNK</c>).</summary>
    public string? CredentialIdentifier { get; set; }

    public string? CredentialSecret { get; set; }
}

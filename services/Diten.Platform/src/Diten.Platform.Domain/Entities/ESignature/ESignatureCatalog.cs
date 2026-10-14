using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.ESignature;

/// <summary>The seven languages every tenant-facing name carries (KS-12).</summary>
public static class ESignatureLanguages
{
    public static readonly IReadOnlyList<string> All = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>Languages whose name is missing or blank, in <see cref="All"/> order.</summary>
    public static IReadOnlyList<string> Missing(IReadOnlyDictionary<string, string>? names) =>
        All.Where(language => names is null || !names.TryGetValue(language, out var value) || string.IsNullOrWhiteSpace(value)).ToList();

    /// <summary>The name in <paramref name="language"/>, falling back to tr, then en (all seven exist once validated).</summary>
    public static (string Text, string Language) Pick(IReadOnlyDictionary<string, string> names, string? language)
    {
        foreach (var candidate in new[] { Normalize(language), "tr", "en" })
        {
            if (candidate is not null && names.TryGetValue(candidate, out var text) && !string.IsNullOrWhiteSpace(text))
            {
                return (text, candidate);
            }
        }

        var first = names.First();
        return (first.Value, first.Key);
    }

    private static string? Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var head = language.Split(',', ';')[0].Trim().Split('-')[0].ToLowerInvariant();
        return All.Contains(head) ? head : null;
    }
}

/// <summary>
/// MOD-0022 — tenant signature meaning (KS-12). <see cref="Code"/> is immutable; once a signature uses it
/// (<see cref="FirstUsedAtUtc"/>) it can no longer be renamed by code and it is never deleted. Name changes affect new
/// signatures only: records keep <see cref="ESignatureRecord.MeaningTextAtSign"/>.
/// </summary>
public sealed class ESignatureMeaning : TenantScopedEntity
{
    public required string Code { get; init; }
    public Dictionary<string, string> Names { get; set; } = new();
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? FirstUsedAtUtc { get; set; }
    public long UsageCount { get; set; }

    public bool IsLocked => FirstUsedAtUtc is not null;

    public static readonly System.Text.RegularExpressions.Regex CodePattern = new("^[A-Z][A-Z0-9_]{1,31}$");
}

/// <summary>
/// MOD-0022 — one signable action of a consumer module (CT-D12): the catalog the policy rows come from. Registered over
/// the internal endpoint by the owning module; a module registers only its own <c>module.</c> prefix. Never deleted.
///
/// <para>GLOBAL, not tenant-scoped: the registration request (ESIGN-BUNDLE §4.23) carries no tenant, and a module's signable
/// actions are the same for every tenant — like the module catalog. What a tenant decides about an action lives in its
/// policy rows, which ARE tenant-scoped.</para>
/// </summary>
public sealed class ESignableAction : GlobalEntity
{
    public required string ActionCode { get; init; }
    public required string Module { get; set; }
    public Dictionary<string, string> Names { get; set; } = new();
    public bool DefaultRequired { get; set; }
    public string? DefaultMeaningCode { get; set; }
    public ESignatureVisualMode DefaultVisualMode { get; set; } = ESignatureVisualMode.Optional;
    public DateTime RegisteredAtUtc { get; set; }
}

/// <summary>
/// MOD-0022 — e-signature policy (KS-2 / KS-3 / KS-5 / KS-8): one tenant default plus 0..n LegalEntity overrides. An
/// override is a full copy; "revert" removes it. Optimistic concurrency on <see cref="BaseEntity.Version"/>.
/// </summary>
public sealed class ESignaturePolicy : TenantScopedEntity
{
    public required ESignaturePolicyScope Scope { get; init; }
    public Guid? LegalEntityId { get; init; }
    public List<ESignaturePolicyRow> Rows { get; set; } = [];
    public bool ReauthEvery { get; set; } = true;
    public int ReauthWindowMinutes { get; set; } = 15;

    /// <summary>KS-7, tenant scope only (CT-D15); echoed read-only on an override.</summary>
    public bool SampleApprovalRequired { get; set; } = true;

    public const int MinWindowMinutes = 1;
    public const int MaxWindowMinutes = 60;
}

public sealed class ESignaturePolicyRow
{
    public required string ActionCode { get; set; }
    public bool Required { get; set; }
    public string? MeaningCode { get; set; }
    public ESignatureVisualMode VisualMode { get; set; } = ESignatureVisualMode.Optional;
    public ESignatureSecondFactor SecondFactor { get; set; } = ESignatureSecondFactor.NotAvailable;
}

/// <summary>
/// MOD-0022 — a signature request (envelope, KS-4): one nominated signer, one meaning, a due date. State changes only
/// through the guarded methods below; every change is an audit event (handlers).
/// </summary>
public sealed class ESignatureRequest : TenantScopedEntity
{
    public required string RequestNumber { get; init; }
    public required string Module { get; init; }
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string SubjectDisplay { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string? ActionCode { get; init; }
    public required string MeaningCode { get; init; }
    public string? Note { get; init; }
    public required string RequestedBy { get; init; }
    public required string RequestedByName { get; init; }
    public required DateTime RequestedAtUtc { get; init; }
    public required string SignerUserId { get; init; }
    public required DateTime DueAtUtc { get; init; }
    public required string IdempotencyKey { get; init; }

    public ESignatureRequestStatus Status { get; set; } = ESignatureRequestStatus.Waiting;
    public Guid? SignatureId { get; set; }
    public string? RejectReason { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    public bool IsOpen => Status == ESignatureRequestStatus.Waiting;

    public void MarkSigned(Guid signatureId, DateTime atUtc)
    {
        EnsureOpen();
        Status = ESignatureRequestStatus.Signed;
        SignatureId = signatureId;
        DecidedAtUtc = atUtc;
    }

    public void Reject(string reason, DateTime atUtc)
    {
        EnsureOpen();
        Status = ESignatureRequestStatus.Rejected;
        RejectReason = reason;
        DecidedAtUtc = atUtc;
    }

    public void Cancel(string reason, DateTime atUtc)
    {
        EnsureOpen();
        Status = ESignatureRequestStatus.Cancelled;
        CancelReason = reason;
        DecidedAtUtc = atUtc;
    }

    public void Expire(DateTime atUtc)
    {
        EnsureOpen();
        Status = ESignatureRequestStatus.Expired;
        DecidedAtUtc = atUtc;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException($"Signature request {RequestNumber} is {Status}, not Waiting.");
        }
    }
}

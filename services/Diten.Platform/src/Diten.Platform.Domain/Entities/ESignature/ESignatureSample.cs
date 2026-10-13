using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.ESignature;

public enum ESignatureSampleKind
{
    Drawn = 0,
    Uploaded = 1
}

public enum ESignatureSampleFormat
{
    Png = 0,
    PngAndVector = 1,
    Vector = 2
}

/// <summary>What happened to a sample. The sample's status is the projection of its LAST event (pack §4).</summary>
public enum ESignatureSampleEventKind
{
    Submitted = 0,
    Approved = 1,
    Rejected = 2,
    Activated = 3,
    Deactivated = 4,
    Withdrawn = 5
}

public enum ESignatureSampleStatus
{
    Pending = 0,
    Active = 1,
    Inactive = 2,
    Rejected = 3,
    Withdrawn = 4
}

/// <summary>
/// MOD-0022 E3 — one version of a user's saved signature image ("İmzam", KS-7 / KS-9 / KS-10). INSERT-ONLY: the bytes, their
/// hash and the ticket that proved the password are fixed at save time; everything that happens later is an
/// <see cref="ESignatureSampleEvent"/>. A sample is never deleted — a used one cannot even be withdrawn — so every
/// signature that shows it keeps pointing at the exact image it was made with.
/// </summary>
public sealed class ESignatureSample : TenantScopedEntity
{
    public required string SampleNumber { get; init; }
    public required string OwnerUserId { get; init; }
    public required string OwnerLogin { get; init; }
    public required string OwnerName { get; init; }
    public required ESignatureSampleKind Kind { get; init; }
    public required ESignatureSampleFormat Format { get; init; }
    public required string ContentId { get; init; }
    public required string StorageProvider { get; init; }
    public required string StoragePath { get; init; }
    public required string ImageHash { get; init; }
    public string? VectorJson { get; init; }
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>SHA-256 of the ticket (intent sample, kind password) spent to save it — never the ticket.</summary>
    public required string TicketRef { get; init; }

    public required string IdempotencyKey { get; init; }
}

/// <summary>
/// MOD-0022 E3 — insert-only event of one sample. <see cref="Sequence"/> is 0, 1, 2… per sample and unique (index), so two
/// parallel decisions on the same sample cannot both be appended: the second insert hits the index and its transaction is
/// aborted.
/// </summary>
public sealed class ESignatureSampleEvent : TenantScopedEntity
{
    public required Guid SampleId { get; init; }
    public required string OwnerUserId { get; init; }
    public required int Sequence { get; init; }
    public required ESignatureSampleEventKind Event { get; init; }
    public required string By { get; init; }
    public required string ByName { get; init; }
    public required DateTime AtUtc { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// MOD-0022 E3 — the per-owner pointer to the ONE active sample (pack §4 "at most one Active per owner"). Not evidence: a
/// compare-and-set guard. Every activation / deactivation moves it inside the same transaction as its events, so two
/// parallel activations for one owner serialize on this document instead of leaving two Active samples.
/// </summary>
public sealed class ESignatureSampleOwnerState : TenantScopedEntity
{
    public required string OwnerUserId { get; init; }
    public Guid? ActiveSampleId { get; set; }
}

/// <summary>The pure rules of samples (pack §8 "Samples", V21 / V22).</summary>
public static class ESignatureSampleRules
{
    public static ESignatureSampleStatus Status(ESignatureSampleEventKind last) => last switch
    {
        ESignatureSampleEventKind.Submitted => ESignatureSampleStatus.Pending,
        ESignatureSampleEventKind.Approved => ESignatureSampleStatus.Active,
        ESignatureSampleEventKind.Activated => ESignatureSampleStatus.Active,
        ESignatureSampleEventKind.Rejected => ESignatureSampleStatus.Rejected,
        ESignatureSampleEventKind.Deactivated => ESignatureSampleStatus.Inactive,
        ESignatureSampleEventKind.Withdrawn => ESignatureSampleStatus.Withdrawn,
        _ => ESignatureSampleStatus.Inactive
    };

    /// <summary>The status after the events (ordered by sequence); a sample without events is pending.</summary>
    public static ESignatureSampleStatus Project(IEnumerable<ESignatureSampleEvent> events)
    {
        var last = events.OrderBy(e => e.Sequence).LastOrDefault();
        return last is null ? ESignatureSampleStatus.Pending : Status(last.Event);
    }

    /// <summary>V21: an approver decides a pending sample of SOMEONE ELSE (CT-D5).</summary>
    public static bool IsSelfDecision(ESignatureSample sample, string actorUserId) =>
        string.Equals(sample.OwnerUserId, actorUserId, StringComparison.Ordinal);

    /// <summary>V22: a sample can be withdrawn only while unused and not active.</summary>
    public static bool CanWithdraw(ESignatureSampleStatus status, long usedCount) =>
        usedCount == 0 && status is not ESignatureSampleStatus.Active and not ESignatureSampleStatus.Withdrawn;
}

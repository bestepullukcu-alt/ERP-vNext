using System.Text;
using System.Text.Json;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.ESignature.Services;

/// <summary>
/// MOD-0022 E3 — samples as the rest of the service sees them: projections for the wire, the activation step every
/// approval / auto-activation shares, and THE port the signing handler uses (pack §8 step 4: "Sample must be the caller's
/// own Active sample").
/// </summary>
public interface IESignatureSampleBook
{
    /// <summary>
    /// The sample the signer may show: it exists in the tenant, belongs to <paramref name="signerUserId"/>, its last event
    /// makes it Active AND it is the owner's current active pointer. Anything else is null (409 ESIGN_SAMPLE_NOT_ACTIVE).
    /// </summary>
    Task<ESignatureSample?> ResolveActiveForSignerAsync(Guid tenantId, string signerUserId, Guid sampleId, CancellationToken ct);

    Task<IReadOnlyList<SampleDto>> ToDtosAsync(Guid tenantId, IReadOnlyList<ESignatureSample> samples, CancellationToken ct);

    /// <summary>The owner's active sample and newest pending one (the signing dialog's mySample / myPendingSample).</summary>
    Task<(SampleDto? Active, SampleDto? Pending)> MineForDialogAsync(Guid tenantId, string ownerUserId, CancellationToken ct);

    /// <summary>
    /// Inside the caller's transaction: appends <paramref name="activation"/> (Approved or Activated) to the sample, appends
    /// Deactivated to the owner's previous active sample, and moves the owner pointer — one active sample per owner.
    /// A stale pointer or a parallel decision aborts the transaction (<see cref="ESignatureDuplicateException"/> /
    /// <see cref="ESignatureSampleConflictException"/>).
    /// </summary>
    Task ActivateAsync(IPlatformTransactionSession session, ESignatureSample sample, ESignatureSampleEventKind activation, int sequence,
        string by, string byName, DateTime atUtc, CancellationToken ct);
}

/// <summary>The owner pointer moved while this transaction ran: someone else (de)activated a sample of the same owner.</summary>
public sealed class ESignatureSampleConflictException() : Exception("The owner's active sample changed meanwhile.");

public sealed class ESignatureSampleBook : IESignatureSampleBook
{
    private readonly IESignatureSampleRepository _samples;
    private readonly IESignatureSampleEventRepository _events;
    private readonly IESignSampleOwnerStateRepository _owners;
    private readonly IESignatureRecordRepository _records;

    public ESignatureSampleBook(IESignatureSampleRepository samples, IESignatureSampleEventRepository events, IESignSampleOwnerStateRepository owners,
        IESignatureRecordRepository records)
    {
        _samples = samples;
        _events = events;
        _owners = owners;
        _records = records;
    }

    public async Task<ESignatureSample?> ResolveActiveForSignerAsync(Guid tenantId, string signerUserId, Guid sampleId, CancellationToken ct)
    {
        var sample = await _samples.GetAsync(tenantId, sampleId, ct);
        // Ownership first: another user's sample — whatever its status — is never the signer's appearance (KS-10).
        if (sample is null || !string.Equals(sample.OwnerUserId, signerUserId, StringComparison.Ordinal))
        {
            return null;
        }

        var status = ESignatureSampleRules.Project(await _events.ListBySampleAsync(tenantId, sample.Id, null, ct));
        if (status != ESignatureSampleStatus.Active)
        {
            return null;
        }

        // The sample must be its OWNER's current pointer; the ownership check above is what ties that owner to the signer.
        var owner = await _owners.GetAsync(null, tenantId, sample.OwnerUserId, ct);
        return owner?.ActiveSampleId == sample.Id ? sample : null;
    }

    public async Task<IReadOnlyList<SampleDto>> ToDtosAsync(Guid tenantId, IReadOnlyList<ESignatureSample> samples, CancellationToken ct)
    {
        if (samples.Count == 0)
        {
            return [];
        }

        var ids = samples.Select(s => s.Id).ToList();
        var events = (await _events.ListBySamplesAsync(tenantId, ids, ct)).GroupBy(e => e.SampleId).ToDictionary(g => g.Key, g => g.ToList());
        var usage = await _records.GetSampleUsageAsync(tenantId, ids, ct);
        return samples.Select(s => ToDto(s, events.GetValueOrDefault(s.Id) ?? [], usage.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<(SampleDto? Active, SampleDto? Pending)> MineForDialogAsync(Guid tenantId, string ownerUserId, CancellationToken ct)
    {
        var mine = await ToDtosAsync(tenantId, await _samples.ListByOwnerAsync(tenantId, ownerUserId, ct), ct);
        var owner = await _owners.GetAsync(null, tenantId, ownerUserId, ct);
        var active = mine.FirstOrDefault(s => s.Id == owner?.ActiveSampleId && s.Status == ESignatureWire.Name(ESignatureSampleStatus.Active));
        var pending = mine.FirstOrDefault(s => s.Status == ESignatureWire.Name(ESignatureSampleStatus.Pending));
        return (active, pending);
    }

    public async Task ActivateAsync(IPlatformTransactionSession session, ESignatureSample sample, ESignatureSampleEventKind activation, int sequence,
        string by, string byName, DateTime atUtc, CancellationToken ct)
    {
        var owner = await _owners.GetAsync(session, sample.TenantId, sample.OwnerUserId, ct);
        var previous = owner?.ActiveSampleId;
        if (previous is { } previousId && previousId != sample.Id)
        {
            var previousEvents = await _events.ListBySampleAsync(sample.TenantId, previousId, session, ct);
            await _events.InsertAsync(session, Event(sample.TenantId, previousId, sample.OwnerUserId, previousEvents.Count,
                ESignatureSampleEventKind.Deactivated, by, byName, atUtc, "replaced by " + sample.SampleNumber), ct);
        }

        await _events.InsertAsync(session, Event(sample.TenantId, sample.Id, sample.OwnerUserId, sequence, activation, by, byName, atUtc, null), ct);
        if (!await _owners.MoveActiveAsync(session, sample.TenantId, sample.OwnerUserId, previous, sample.Id, ct))
        {
            throw new ESignatureSampleConflictException();
        }
    }

    internal static ESignatureSampleEvent Event(Guid tenantId, Guid sampleId, string ownerUserId, int sequence, ESignatureSampleEventKind kind,
        string by, string byName, DateTime atUtc, string? reason) => new()
    {
        TenantId = tenantId,
        CreatedBy = by,
        SampleId = sampleId,
        OwnerUserId = ownerUserId,
        Sequence = sequence,
        Event = kind,
        By = by,
        ByName = byName,
        AtUtc = atUtc,
        Reason = reason
    };

    internal static SampleDto ToDto(ESignatureSample s, IReadOnlyList<ESignatureSampleEvent> events, long usedCount)
    {
        var decision = events
            .Where(e => e.Event is ESignatureSampleEventKind.Approved or ESignatureSampleEventKind.Rejected)
            .OrderBy(e => e.Sequence)
            .LastOrDefault();
        return new SampleDto(
            s.Id,
            s.SampleNumber,
            new SampleOwnerDto(s.OwnerUserId, s.OwnerLogin, s.OwnerName),
            ESignatureWire.Name(s.Kind),
            ESignatureWire.Name(s.Format),
            ESignatureWire.Name(ESignatureSampleRules.Project(events)),
            s.ImageHash,
            s.CreatedAtUtc,
            usedCount,
            decision is null ? null : new SampleDecisionInfoDto(decision.By, decision.ByName, decision.AtUtc, decision.Reason));
    }
}

/// <summary>
/// The image checks a sample shares with a drawn signature (pack §8 "Images", V8–V10): PNG bytes (an upload is converted to
/// PNG by the browser — JPG bytes are refused), ≤ 1 MiB, IHDR ≤ 2000 × 800; strokes ≤ 256 KB of points in 0..1.
/// </summary>
public static class ESignatureImageInput
{
    private const int MaxStrokesBytes = 262_144;

    public enum Failure
    {
        None = 0,
        Empty = 1,
        NotPng = 2,
        TooLarge = 3,
        BadStrokes = 4
    }

    public static (byte[]? Png, Failure Failure) ReadPng(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return (null, Failure.Empty);
        }

        if (base64.Length > (ESignaturePngValidator.MaxBytes + 2) / 3 * 4 + 4)
        {
            return (null, Failure.TooLarge);
        }

        byte[] png;
        try
        {
            png = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return (null, Failure.NotPng);
        }

        return ESignaturePngValidator.Check(png) switch
        {
            PngCheck.Ok => (png, Failure.None),
            PngCheck.Empty => (null, Failure.Empty),
            PngCheck.TooLarge => (null, Failure.TooLarge),
            _ => (null, Failure.NotPng)
        };
    }

    public static bool StrokesValid(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxStrokesBytes)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var stroke in document.RootElement.EnumerateArray())
            {
                if (stroke.ValueKind != JsonValueKind.Array) return false;
                foreach (var point in stroke.EnumerateArray())
                {
                    if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2) return false;
                    foreach (var coordinate in point.EnumerateArray())
                    {
                        if (coordinate.ValueKind != JsonValueKind.Number) return false;
                        var value = coordinate.GetDouble();
                        if (value is < 0 or > 1) return false;
                    }
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

// WP-CL-BE-5 — claim evidence lives in MOD-0031 (Platform evidence links), pinned to a document VERSION. CRM owns the
// ObjectRef (never taken from a client), the lock (evidence changes only in draft), inheritance (a country version sees
// its bound claim record's links), the "≥1 evidence to submit" rule, the copy onto a new version, and the read-time
// comparison that turns an approved record review-required when a linked document moved on (DocMgmt publishes no
// change event, so the comparison happens on read).

/// <summary>WP-CL-BE-5 — the ObjectRef CRM stamps on a MOD-0031 link.</summary>
public sealed record ClaimEvidenceObjectRef(string Module, string ObjectType, string ObjectId, string? ObjectVersion);

public sealed record ClaimEvidenceLocator(string? Section, string? Page, string? Table, string? Quote);

public sealed record ClaimEvidenceSpan(string LanguageCode, string Text, int? Start, int? End);

/// <summary>One MOD-0031 link as CRM reads it, including the document state Platform computes on read.</summary>
public sealed record ClaimEvidenceLink(
    Guid LinkId,
    ClaimEvidenceObjectRef ObjectRef,
    string DocumentKind,
    Guid DocumentId,
    Guid? DocumentVersionId,
    string? DocumentVersionLabel,
    string DocumentTitle,
    string EvidenceTypeCode,
    ClaimEvidenceLocator Locator,
    IReadOnlyList<ClaimEvidenceSpan> SupportedSpans,
    string Status,
    string? LinkedBy,
    DateTimeOffset LinkedAt,
    string? RemovedBy,
    DateTimeOffset? RemovedAt,
    string? RemovalReason,
    Guid? CurrentVersionId,
    string? CurrentVersionLabel,
    bool IsSuperseded,
    string? DocumentState,
    DateTimeOffset? ReviewDueAt,
    // WP-CL-FIX-1 — the document code people recognise (MOD-0031 read; null from an older Platform).
    string? DocumentCode = null)
{
    public bool IsActive => string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);
}

/// <summary>The body of a new link; the ObjectRef is added by CRM.</summary>
public sealed record ClaimEvidenceLinkInput(
    string? DocumentKind,
    Guid DocumentId,
    Guid? DocumentVersionId,
    string? EvidenceTypeCode,
    ClaimEvidenceLocator? Locator,
    IReadOnlyList<ClaimEvidenceSpan>? SupportedSpans);

public sealed record ClaimEvidenceDocumentOptionDto(
    string Kind,
    Guid DocumentId,
    string Title,
    string? Code,
    string? DocumentType,
    Guid? CurrentVersionId,
    string? CurrentVersionLabel,
    string Status,
    DateTimeOffset? EffectiveDate,
    string? CountryCode,
    string? SourceVersion,
    string? SourceStatus,
    // WP-CL-FIX-1 — lifecycle state (effective | suspended | retired | withdrawn | unknown); null from an older Platform.
    string? DocumentState = null);

public enum ClaimEvidenceCallOutcome
{
    Ok,
    NotFound,
    Forbidden,
    Rejected,
    Unavailable
}

/// <summary>A MOD-0031 answer: on a refusal, Platform's status + reason code + message are carried through.</summary>
public sealed record ClaimEvidenceCallResult<T>(
    ClaimEvidenceCallOutcome Outcome, T? Data, int Status, string? ReasonCode, string? Message);

/// <summary>
/// WP-CL-BE-5 — CRM's view of MOD-0031, reached through the Gateway with the CALLER's <c>Authorization</c> +
/// <c>X-Tenant-Id</c> (the BE-4 pattern): the user linking evidence must be able to read the document, so MOD-0031's
/// document gate applies to them — never to a service identity.
/// </summary>
public interface IClaimEvidenceClient
{
    Task<ClaimEvidenceCallResult<ClaimEvidenceLink>> LinkAsync(
        ClaimEvidenceObjectRef objectRef, ClaimEvidenceLinkInput input, CancellationToken ct);

    Task<ClaimEvidenceCallResult<ClaimEvidenceLink>> GetAsync(Guid linkId, CancellationToken ct);

    Task<ClaimEvidenceCallResult<ClaimEvidenceLink>> RemoveAsync(Guid linkId, string? reason, CancellationToken ct);

    /// <summary>Links of many objects (the client splits into ≤100-object calls). Null when MOD-0031 cannot be
    /// reached or refuses the read.</summary>
    Task<IReadOnlyList<ClaimEvidenceLink>?> QueryAsync(
        IReadOnlyList<ClaimEvidenceObjectRef> objects, bool includeRemoved, CancellationToken ct);

    Task<ClaimEvidenceCallResult<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>> GetDocumentOptionsAsync(
        string? search, string? kind, CancellationToken ct);
}

/// <summary>WP-CL-BE-5 — fixed rules of claim evidence.</summary>
public static class ClaimEvidenceRules
{
    public const string Module = "crm";
    public const string ClaimObjectType = "claim";
    public const string CountryVersionObjectType = "claim-country-version";
    public const string OriginCore = "core";
    public const string OriginLocal = "local";

    /// <summary>A linked document state that makes an approved record review-required.</summary>
    public static readonly IReadOnlySet<string> ReviewStates =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "suspended", "retired", "withdrawn" };

    public static ClaimEvidenceObjectRef For(Claim c) =>
        new(Module, ClaimObjectType, c.Id.ToString("D"), c.ClaimVersion);

    public static ClaimEvidenceObjectRef For(ClaimCountryVersion v) =>
        new(Module, CountryVersionObjectType, v.Id.ToString("D"), v.CountryVersion);

    /// <summary>The read key of an object: every link of the record (all business versions of it).</summary>
    public static ClaimEvidenceObjectRef ReadKey(string objectType, Guid id) => new(Module, objectType, id.ToString("D"), null);

    public static string Key(string objectType, string objectId) => $"{objectType}:{objectId.ToLowerInvariant()}";

    /// <summary>Evidence changes only while the record is a draft (a change after that is a new version).</summary>
    public static bool IsLocked(string status, bool archived) => archived || status != ClaimStatuses.Draft;

    public static bool NeedsReview(ClaimEvidenceLink link) =>
        link.IsActive && (link.IsSuperseded || (link.DocumentState is { } s && ReviewStates.Contains(s)));

    public static bool IsExpiring(ClaimEvidenceLink link, DateTimeOffset now, int windowDays) =>
        link.IsActive && link.ReviewDueAt is { } due && due <= now.AddDays(windowDays);

    public static Response<T> Locked<T>() => Response<T>.Fail(
        new[] { ClaimErrorCodes.EvidenceLocked, "Evidence can only change on a draft; open a new version to change it." },
        409);

    public static Response<T> Required<T>() => Response<T>.Fail(
        new[] { ClaimErrorCodes.EvidenceRequired, "At least one active evidence link is required before review." },
        409);

    public static Response<T> Unavailable<T>() => Response<T>.Fail(
        new[] { ClaimErrorCodes.EvidenceUnavailable, "The evidence service cannot be reached. Nothing was changed." },
        503);

    /// <summary>A MOD-0031 refusal, passed through with its own status and reason code.</summary>
    public static Response<T> FromCall<T, TData>(ClaimEvidenceCallResult<TData> result) => result.Outcome switch
    {
        ClaimEvidenceCallOutcome.Unavailable => Unavailable<T>(),
        ClaimEvidenceCallOutcome.NotFound => Response<T>.Fail(
            new[] { result.ReasonCode ?? "not_found", result.Message ?? "Not found." }, 404),
        ClaimEvidenceCallOutcome.Forbidden => Response<T>.Fail(
            new[] { result.ReasonCode ?? "evidence_forbidden", result.Message ?? "Not allowed." },
            result.Status is 401 or 403 ? result.Status : 403),
        _ => Response<T>.Fail(
            new[] { result.ReasonCode ?? "evidence_rejected", result.Message ?? "The evidence service refused the request." },
            result.Status is >= 400 and < 500 ? result.Status : 400)
    };
}

/// <summary>
/// WP-CL-BE-5 — the evidence of a set of records, read in one bulk MOD-0031 call. A claim record's effective evidence is
/// its own active links; a country version's is its bound claim record's active links (inherited, origin core) plus its
/// own (origin local).
/// </summary>
public sealed class ClaimEvidenceSnapshot
{
    private readonly IReadOnlyDictionary<string, List<ClaimEvidenceLink>> _byObject;
    private readonly DateTimeOffset _now;
    private readonly int _windowDays;

    public ClaimEvidenceSnapshot(IEnumerable<ClaimEvidenceLink> links, DateTimeOffset now, int windowDays)
    {
        _byObject = links.GroupBy(l => ClaimEvidenceRules.Key(l.ObjectRef.ObjectType, l.ObjectRef.ObjectId))
            .ToDictionary(g => g.Key, g => g.ToList());
        _now = now;
        _windowDays = windowDays;
    }

    public DateTimeOffset Now => _now;

    public int WindowDays => _windowDays;

    public IReadOnlyList<ClaimEvidenceLink> Own(string objectType, Guid id) =>
        _byObject.TryGetValue(ClaimEvidenceRules.Key(objectType, id.ToString("D")), out var list)
            ? list
            : Array.Empty<ClaimEvidenceLink>();

    public IReadOnlyList<ClaimEvidenceLink> Effective(Claim c) =>
        Own(ClaimEvidenceRules.ClaimObjectType, c.Id).Where(l => l.IsActive).ToList();

    public IReadOnlyList<ClaimEvidenceLink> Inherited(ClaimCountryVersion v) =>
        Own(ClaimEvidenceRules.ClaimObjectType, v.ClaimId).Where(l => l.IsActive).ToList();

    public IReadOnlyList<ClaimEvidenceLink> Effective(ClaimCountryVersion v) =>
        Inherited(v).Concat(Own(ClaimEvidenceRules.CountryVersionObjectType, v.Id).Where(l => l.IsActive)).ToList();

    public bool IsExpiring(Claim c) => Effective(c).Any(l => ClaimEvidenceRules.IsExpiring(l, _now, _windowDays));

    public bool IsExpiring(ClaimCountryVersion v) =>
        Effective(v).Any(l => ClaimEvidenceRules.IsExpiring(l, _now, _windowDays));
}

/// <summary>
/// WP-CL-BE-5 — read-time evidence evaluation (the BE-4 reconcile point): one bulk MOD-0031 read for the live records,
/// then every APPROVED record whose effective evidence has a superseded / suspended / retired / withdrawn document turns
/// review-required. <c>ApprovedAt</c> is kept, nothing else is propagated (not the BE-1 core → country rule), and a
/// record already review-required is left alone (idempotent). MOD-0031 being unreachable never breaks the read.
/// </summary>
public sealed class ClaimEvidenceReviewer
{
    public const string EvidenceActor = "evidence:mod-0031";

    private readonly IClaimEvidenceClient _client;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _versions;
    private readonly IContentCompositionAuditPublisher? _audit;
    private readonly IClaimCoverageSettings? _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<ClaimEvidenceReviewer> _logger;

    public ClaimEvidenceReviewer(
        IClaimEvidenceClient client,
        IClaimRepository claims,
        IClaimCountryVersionRepository versions,
        IContentCompositionAuditPublisher? audit = null,
        IClaimCoverageSettings? settings = null,
        TimeProvider? clock = null,
        ILogger<ClaimEvidenceReviewer>? logger = null)
    {
        _client = client;
        _claims = claims;
        _versions = versions;
        _audit = audit;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<ClaimEvidenceReviewer>.Instance;
    }

    /// <summary>One bulk read for the live (not archived, not inactive) records — or, with <paramref name="allClaims"/>
    /// (the WP-CL-FE-1 list counters), every listed claim record. Null when MOD-0031 is unavailable.</summary>
    public async Task<ClaimEvidenceSnapshot?> LoadAsync(
        IEnumerable<Claim> claims, IEnumerable<ClaimCountryVersion> versions, CancellationToken ct, bool allClaims = false)
    {
        var objects = new Dictionary<string, ClaimEvidenceObjectRef>();
        void Add(string type, Guid id) => objects.TryAdd(ClaimEvidenceRules.Key(type, id.ToString("D")),
            ClaimEvidenceRules.ReadKey(type, id));

        foreach (var c in claims.Where(c => allClaims || (!c.IsArchived() && c.Status != ClaimStatuses.Inactive)))
        {
            Add(ClaimEvidenceRules.ClaimObjectType, c.Id);
        }

        foreach (var v in versions.Where(v => !v.IsArchived() && v.Status != ClaimStatuses.Inactive))
        {
            Add(ClaimEvidenceRules.CountryVersionObjectType, v.Id);
            Add(ClaimEvidenceRules.ClaimObjectType, v.ClaimId); // inherited evidence
        }

        var now = _clock.GetUtcNow();
        var window = _settings?.ExpiringWindowDays ?? ClaimCoverageDefaults.ExpiringWindowDays;
        if (objects.Count == 0)
        {
            return new ClaimEvidenceSnapshot([], now, window);
        }

        var links = await _client.QueryAsync(objects.Values.ToList(), includeRemoved: false, ct);
        if (links is null)
        {
            _logger.LogInformation("claims.evidence.read_unavailable Objects={Count}", objects.Count);
            return null;
        }

        return new ClaimEvidenceSnapshot(links, now, window);
    }

    /// <summary>Approved records with a changed document → review-required. Returns how many changed.</summary>
    public async Task<int> FlagReviewRequiredAsync(Guid tenantId, IEnumerable<Claim> claims,
        IEnumerable<ClaimCountryVersion> versions, ClaimEvidenceSnapshot snapshot, CancellationToken ct)
    {
        var changed = 0;
        var now = _clock.GetUtcNow();
        foreach (var claim in claims.Where(c => c.Status == ClaimStatuses.Approved && !c.IsArchived()))
        {
            if (snapshot.Effective(claim).FirstOrDefault(ClaimEvidenceRules.NeedsReview) is not { } trigger)
            {
                continue;
            }

            claim.Status = ClaimStatuses.ReviewRequired; // ApprovedAt / ApprovedBy stay
            claim.UpdatedAt = now;
            claim.UpdatedBy = EvidenceActor;
            await _claims.UpdateAsync(claim, ct);
            await PublishAsync(tenantId, ContentCompositionAuditEntities.Claim, claim.Id, claim.Version,
                claim.ClaimCode, trigger, ct);
            changed++;
        }

        foreach (var version in versions.Where(v => v.Status == ClaimStatuses.Approved && !v.IsArchived()))
        {
            if (snapshot.Effective(version).FirstOrDefault(ClaimEvidenceRules.NeedsReview) is not { } trigger)
            {
                continue;
            }

            version.Status = ClaimStatuses.ReviewRequired;
            version.UpdatedAt = now;
            version.UpdatedBy = EvidenceActor;
            await _versions.UpdateAsync(version, ct);
            await PublishAsync(tenantId, ContentCompositionAuditEntities.ClaimCountryVersion, version.Id,
                version.Version, $"{version.ClaimCode}|{version.CountryCode}", trigger, ct);
            changed++;
        }

        return changed;
    }

    private Task PublishAsync(Guid tenantId, string entityType, Guid id, int version, string code,
        ClaimEvidenceLink trigger, CancellationToken ct)
        => _audit is null
            ? Task.CompletedTask
            : _audit.PublishAsync(ClaimReasonCodes.ReviewRequiredEvidenceChanged, tenantId, entityType, id, version,
                $"{code}|link={trigger.LinkId:D}|document={trigger.DocumentId:D}", ct);
}

/// <summary>WP-CL-BE-5 — the read handlers' guard around the evidence evaluation: never lets MOD-0031 break a read.
/// <c>Changed</c> = records turned review-required (the caller reloads); <c>Snapshot</c> = null when unavailable.</summary>
internal static class ClaimReadEvidence
{
    public static async Task<(bool Changed, ClaimEvidenceSnapshot? Snapshot)> RunAsync(ClaimEvidenceReviewer? reviewer,
        Guid tenantId, IReadOnlyCollection<Claim> claims, IReadOnlyCollection<ClaimCountryVersion> versions,
        CancellationToken ct, bool allClaims = false)
    {
        if (reviewer is null)
        {
            return (false, null);
        }

        try
        {
            var snapshot = await reviewer.LoadAsync(claims, versions, ct, allClaims);
            if (snapshot is null)
            {
                return (false, null);
            }

            return (await reviewer.FlagReviewRequiredAsync(tenantId, claims, versions, snapshot, ct) > 0, snapshot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, null);
        }
    }
}

/// <summary>WP-CL-BE-5 — the submit rule: at least one active effective evidence link (a claim: its own; a country
/// version: inherited + own). Fail-closed: MOD-0031 unreachable (or no client) is 503, never a silent pass.</summary>
internal static class ClaimEvidenceGate
{
    public static async Task<Response<T>?> RequireAsync<T>(IClaimEvidenceClient? client,
        IReadOnlyList<ClaimEvidenceObjectRef> objects, CancellationToken ct)
    {
        if (client is null)
        {
            return ClaimEvidenceRules.Unavailable<T>();
        }

        IReadOnlyList<ClaimEvidenceLink>? links;
        try
        {
            links = await client.QueryAsync(objects, includeRemoved: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            links = null;
        }

        if (links is null)
        {
            return ClaimEvidenceRules.Unavailable<T>();
        }

        var wanted = objects.Select(o => ClaimEvidenceRules.Key(o.ObjectType, o.ObjectId)).ToHashSet();
        return links.Any(l => l.IsActive && wanted.Contains(ClaimEvidenceRules.Key(l.ObjectRef.ObjectType, l.ObjectRef.ObjectId)))
            ? null
            : ClaimEvidenceRules.Required<T>();
    }
}

/// <summary>
/// WP-CL-BE-5 — on a new version, the previous record's OWN active links are linked again under the new ObjectRef (the
/// pinned document versions are kept). Never fails the new version: what could not be copied is logged and audited, and
/// the submit rule (<c>evidence_required</c>) catches a version left without evidence.
/// </summary>
internal static class ClaimEvidenceCopy
{
    public static async Task<(int Copied, int Failed)> CopyAsync(IClaimEvidenceClient? client,
        IContentCompositionAuditPublisher? audit, ILogger? logger, Guid tenantId, ClaimEvidenceObjectRef source,
        ClaimEvidenceObjectRef target, string entityType, Guid targetId, int targetVersion, string code,
        CancellationToken ct)
    {
        if (client is null)
        {
            return (0, 0);
        }

        var copied = 0;
        var failed = 0;
        try
        {
            var links = await client.QueryAsync(
                [new ClaimEvidenceObjectRef(source.Module, source.ObjectType, source.ObjectId, null)], false, ct);
            if (links is null)
            {
                failed = -1; // unknown: the source could not be read
            }
            else
            {
                foreach (var link in links.Where(l => l.IsActive && l.ObjectRef.ObjectType == source.ObjectType
                             && string.Equals(l.ObjectRef.ObjectId, source.ObjectId, StringComparison.OrdinalIgnoreCase)))
                {
                    var result = await client.LinkAsync(target, new ClaimEvidenceLinkInput(
                        link.DocumentKind, link.DocumentId, link.DocumentVersionId, link.EvidenceTypeCode, link.Locator,
                        link.SupportedSpans), ct);
                    if (result.Outcome == ClaimEvidenceCallOutcome.Ok)
                    {
                        copied++;
                    }
                    else
                    {
                        failed++;
                        logger?.LogWarning(
                            "claims.evidence.copy_failed Target={Target} Link={LinkId} Status={Status} Reason={Reason}",
                            target.ObjectId, link.LinkId, result.Status, result.ReasonCode);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "claims.evidence.copy_failed Target={Target}", target.ObjectId);
            failed = failed == 0 ? -1 : failed;
        }

        if (audit is not null && (copied > 0 || failed != 0))
        {
            await audit.PublishAsync(ClaimReasonCodes.EvidenceCopied, tenantId, entityType, targetId, targetVersion,
                $"{code}|from={source.ObjectId}|copied={copied}|failed={(failed < 0 ? "unknown" : failed.ToString())}",
                ct);
        }

        return (copied, failed);
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// A separate audit-only surface. It does not grant planning or MRP consumption.
public sealed class InvalidatedHistoryReader : IInternalInvalidatedHistoryReader
{
    private const string CursorVersion = "MOD0188-AUDIT-CURSOR-1";
    private const int MaximumPageSize = 200;
    private readonly PublishedRevisionMongoStore _published;
    private readonly IInternalInvalidatedHistoryAuthority _authority;
    private readonly TimeProvider _clock;
    private readonly byte[]? _signingKey;
    private readonly DemandPlanningMongoContext? _context;

    public InvalidatedHistoryReader(PublishedRevisionMongoStore published,
        IInternalInvalidatedHistoryAuthority authority,
        IConfiguration? configuration, TimeProvider clock,
        DemandPlanningMongoContext? context)
    {
        _published = published;
        _authority = authority;
        _clock = clock;
        _context = context;
        var key = configuration?["DemandPlanning:SnapshotCursorSigningKey"];
        _signingKey = string.IsNullOrEmpty(key) ||
            Encoding.UTF8.GetByteCount(key) < 32 ? null : Encoding.UTF8.GetBytes(key);
    }

    // Existing internal tests use the full historical reader without a cursor.
    public InvalidatedHistoryReader(PublishedRevisionMongoStore published,
        IInternalInvalidatedHistoryAuthority authority)
        : this(published, authority, null,
            TimeProvider.System, null) { }

    public const string UnusableWarning =
        "Invalidated/geçersiz plan: planlama için kullanılamaz; MRP için kullanılamaz; yalnız tarihsel/audit incelemesi.";

    public async Task<SnapshotReadResult<InternalInvalidatedHistory>> ReadAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty ||
            revisionId == Guid.Empty || actorId == Guid.Empty)
            return new(SnapshotReadOutcome.NotFound);
        InvalidatedHistoryAuthorityEvidence evidence;
        try
        {
            evidence = await _authority.VerifyAsync(tenantId, legalEntityId,
                revisionId, actorId, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(SnapshotReadOutcome.AuthorityUnavailable);
        }
        if (!evidence.SourceAvailable)
            return new(SnapshotReadOutcome.AuthorityUnavailable);
        if (!evidence.HasAuditReadPermission)
            return new(SnapshotReadOutcome.PermissionDenied);
        if (!evidence.RevisionScopeVerified || evidence.TenantId != tenantId ||
            evidence.LegalEntityId != legalEntityId || evidence.RevisionId != revisionId ||
            evidence.AuthorizedSeries is null || evidence.AuthorizedSeries.Count == 0 ||
            evidence.AuthorizedSeries.Distinct().Count() != evidence.AuthorizedSeries.Count)
            return new(SnapshotReadOutcome.NotFound);
        (PublishedRevisionManifest Manifest, PublishedRevisionPart[] Parts)? snapshot;
        try
        {
            snapshot = await _published.ReadSnapshotAsync(tenantId,
                legalEntityId, revisionId, cancellationToken);
        }
        catch (InvalidDataException)
        {
            return new(SnapshotReadOutcome.InvalidSnapshot);
        }
        catch (MongoException)
        {
            return new(SnapshotReadOutcome.StoreUnavailable);
        }
        if (snapshot is null)
            return new(SnapshotReadOutcome.NotFound);
        var (manifest, parts) = snapshot.Value;
        if (manifest.SelectedSeries.Count != evidence.AuthorizedSeries.Count ||
            !manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId))
                .ToHashSet().SetEquals(evidence.AuthorizedSeries))
            return new(SnapshotReadOutcome.NotFound);
        if (manifest.State != DemandRevisionState.Invalidated)
            return new(SnapshotReadOutcome.StateDenied);
        var rows = parts.OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(x => x.WarehouseId, StringComparer.Ordinal)
            .SelectMany(part => part.Rows.OrderBy(x => x.Number).Select(row =>
                new InternalSnapshotRow(part.SkuId, part.WarehouseId,
                    part.BaseUomId, row.Number, row.WeekStart, row.WeekEnd,
                    row.ValueKind, row.Quantity, row.Source,
                    row.ManualReason))).ToArray();
        var status = new InternalSnapshotStatus(manifest.TenantId,
            manifest.LegalEntityId, manifest.RevisionId,
            manifest.PlanningCycleId, manifest.PlanningPeriodKey,
            manifest.State, manifest.StateVersion, manifest.ContentVersion,
            manifest.ChecksumScheme, manifest.Checksum,
            manifest.ExpectedPartCount, manifest.ExpectedRowCount,
            manifest.AsOfDate, manifest.CalendarId,
            manifest.CalendarVersion, manifest.TimeZoneId,
            manifest.HorizonStart, manifest.HorizonEnd,
            manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId)).ToArray());
        var historicalManifest = new InternalSnapshotManifest(status,
            manifest.AsOfDate, manifest.CalendarId,
            manifest.CalendarVersion, manifest.TimeZoneId,
            manifest.HorizonStart, manifest.HorizonEnd,
            manifest.Weeks.Select(x => new PlanningWeek
            {
                Number = x.Number, WeekStart = x.WeekStart,
                WeekEnd = x.WeekEnd
            }).ToArray(),
            manifest.SelectedSeries.Select(x =>
                (x.SkuId, x.WarehouseId, x.BaseUomId)).ToArray(),
            manifest.ExcludedSeries.ToArray(), manifest.CreatedBy,
            manifest.PreparedAt, manifest.SignificantEditorIds.ToArray(),
            manifest.ReviewedBy, manifest.ReviewedAt,
            manifest.ReviewReason, manifest.PublishedBy,
            manifest.PublishedAt);
        RevisionInvalidationView? invalidation = null;
        if (_context is not null)
        {
            List<ManualDraftPublicationAuditRecord> transitions;
            try
            {
                transitions = await _context.ManualDraftPublicationAudit.Find(x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    x.RevisionId == revisionId &&
                    x.NewState == DemandRevisionState.Invalidated &&
                    x.StateVersionAfter == manifest.StateVersion && !x.IsDeleted)
                    .Limit(2).ToListAsync(cancellationToken);
            }
            catch (MongoException)
            {
                return new(SnapshotReadOutcome.StoreUnavailable);
            }
            if (transitions.Count != 1 ||
                string.IsNullOrWhiteSpace(transitions[0].Reason) ||
                string.IsNullOrWhiteSpace(transitions[0].ImpactCode) ||
                string.IsNullOrWhiteSpace(transitions[0].EvidenceReference) ||
                transitions[0].ActorId == Guid.Empty ||
                transitions[0].OccurredAt == default)
                return new(SnapshotReadOutcome.InvalidSnapshot);
            var transition = transitions[0];
            invalidation = new RevisionInvalidationView(
                transition.ImpactCode!, transition.Reason!,
                transition.EvidenceReference!, transition.ActorId,
                transition.OccurredAt, manifest.StateVersion);
        }
        return new(SnapshotReadOutcome.Found,
            new InternalInvalidatedHistory(revisionId, manifest.StateVersion,
                manifest.ChecksumScheme, manifest.Checksum, UnusableWarning,
                historicalManifest, rows, invalidation));
    }

    public async Task<SnapshotReadResult<InternalInvalidatedHistoryPage>> ReadPageAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, Guid actorId,
        int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        if (pageSize is < 1 or > MaximumPageSize)
            return new(SnapshotReadOutcome.InvalidCursor);
        if (_signingKey is null)
            return new(SnapshotReadOutcome.AuthorityUnavailable);
        var result = await ReadAsync(tenantId, legalEntityId, revisionId,
            actorId, cancellationToken);
        if (result.Outcome != SnapshotReadOutcome.Found || result.Data is null)
            return new(result.Outcome);
        var history = result.Data;
        if (history.Invalidation is null)
            return new(SnapshotReadOutcome.InvalidSnapshot);
        var position = 0;
        if (!string.IsNullOrEmpty(cursor))
        {
            var decoded = Decode(cursor);
            if (decoded is null || decoded.Version != CursorVersion ||
                decoded.TenantId != tenantId ||
                decoded.LegalEntityId != legalEntityId ||
                decoded.RevisionId != revisionId || decoded.ActorId != actorId ||
                decoded.Checksum != history.Checksum ||
                decoded.StateVersion != history.StateVersion ||
                decoded.PageSize != pageSize ||
                decoded.Position is <= 0 ||
                decoded.Position >= history.Rows.Count ||
                decoded.PreviousRowKey != RowKey(history.Rows[decoded.Position - 1]) ||
                decoded.ExpiresAt <= _clock.GetUtcNow())
                return new(SnapshotReadOutcome.InvalidCursor);
            position = decoded.Position;
        }
        var rows = history.Rows.Skip(position).Take(pageSize).ToArray();
        var next = position + rows.Length;
        var nextCursor = next < history.Rows.Count
            ? Encode(new CursorPayload(CursorVersion, tenantId, legalEntityId,
                revisionId, actorId, history.Checksum, history.StateVersion,
                pageSize, next, RowKey(rows[^1]),
                _clock.GetUtcNow().AddMinutes(30))) : null;
        return new(SnapshotReadOutcome.Found,
            new InternalInvalidatedHistoryPage(history.Warning,
                history.Manifest.Status, rows, nextCursor));
    }

    private static string RowKey(InternalSnapshotRow row) =>
        $"{row.SkuId:D}|{row.WarehouseId}|{row.WeekNumber:D2}";

    private string Encode(CursorPayload payload)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = HMACSHA256.HashData(_signingKey!, data);
        return $"{Base64Url(data)}.{Base64Url(signature)}";
    }

    private CursorPayload? Decode(string cursor)
    {
        if (cursor.Length > 4096) return null;
        var segments = cursor.Split('.');
        if (segments.Length != 2) return null;
        try
        {
            var data = FromBase64Url(segments[0]);
            var signature = FromBase64Url(segments[1]);
            var expected = HMACSHA256.HashData(_signingKey!, data);
            if (signature.Length != expected.Length ||
                !CryptographicOperations.FixedTimeEquals(signature, expected))
                return null;
            return JsonSerializer.Deserialize<CursorPayload>(data);
        }
        catch (Exception error) when (error is FormatException or JsonException)
        {
            return null;
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }

    private sealed record CursorPayload(string Version, Guid TenantId,
        Guid LegalEntityId, Guid RevisionId, Guid ActorId, string Checksum,
        int StateVersion, int PageSize, int Position,
        string PreviousRowKey, DateTimeOffset ExpiresAt);
}

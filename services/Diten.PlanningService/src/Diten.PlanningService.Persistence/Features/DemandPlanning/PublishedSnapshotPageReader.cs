using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// Every read re-verifies the entire revision in one Mongo snapshot. The low-level
// store is not a planning read surface and does not authorize a consumer.
public sealed class PublishedSnapshotPageReader(
    PublishedRevisionMongoStore store, IInternalSnapshotReadAuthority authority,
    IConfiguration configuration, TimeProvider clock) : IInternalPublishedSnapshotReader
{
    private const string CursorVersion = "MOD0188-SNAPSHOT-CURSOR-1";
    private const int MaximumPageSize = 500;
    private static readonly TimeSpan CursorLifetime = TimeSpan.FromMinutes(30);
    private readonly byte[]? _signingKey = GetSigningKey(configuration);

    public Task<SnapshotReadResult<InternalSnapshotStatus>> ReadStatusAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken) => ReadAsync(tenantId, legalEntityId,
            revisionId, actorId, (manifest, _) => Status(manifest), cancellationToken);

    public Task<SnapshotReadResult<InternalSnapshotManifest>> ReadManifestAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken) => ReadAsync(tenantId, legalEntityId,
            revisionId, actorId, (manifest, _) => new InternalSnapshotManifest(
                Status(manifest), manifest.AsOfDate, manifest.CalendarId,
                manifest.CalendarVersion, manifest.TimeZoneId, manifest.HorizonStart,
                manifest.HorizonEnd, manifest.Weeks.Select(x => new PlanningWeek
                {
                    Number = x.Number, WeekStart = x.WeekStart, WeekEnd = x.WeekEnd
                }).ToArray(),
                manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId, x.BaseUomId))
                    .ToArray(), manifest.ExcludedSeries.ToArray(),
                manifest.CreatedBy, manifest.PreparedAt,
                manifest.SignificantEditorIds.ToArray(),
                manifest.ReviewedBy, manifest.ReviewedAt, manifest.ReviewReason,
                manifest.PublishedBy, manifest.PublishedAt), cancellationToken);

    public async Task<SnapshotReadResult<InternalSnapshotPage>> ReadPageAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId, int pageSize,
        string? cursor, CancellationToken cancellationToken)
    {
        if (pageSize is < 1 or > MaximumPageSize)
            return new(SnapshotReadOutcome.InvalidCursor);
        if (_signingKey is null)
            return new(SnapshotReadOutcome.AuthorityUnavailable);
        // A page can be returned only after the complete manifest and every part
        // have been read and checked in the same Mongo snapshot transaction.
        return await ReadAsync(tenantId, legalEntityId, revisionId, actorId,
            (manifest, parts) => BuildPage(manifest, parts, pageSize, cursor),
            cancellationToken);
    }

    private async Task<SnapshotReadResult<T>> ReadAsync<T>(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        Func<PublishedRevisionManifest, PublishedRevisionPart[], T?> project,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty ||
            revisionId == Guid.Empty || actorId == Guid.Empty)
            return new(SnapshotReadOutcome.NotFound);
        // The revision-bound scope proof is resolved independently of the
        // manifest. It must be checked before any record/status/integrity read.
        SnapshotReadAuthorityEvidence evidence;
        try
        {
            evidence = await authority.VerifyAsync(tenantId, legalEntityId,
                revisionId, actorId, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(SnapshotReadOutcome.AuthorityUnavailable);
        }
        if (!evidence.SourceAvailable)
            return new(SnapshotReadOutcome.AuthorityUnavailable);
        if (!evidence.HasReadPermission)
            return new(SnapshotReadOutcome.PermissionDenied);
        if (!evidence.RevisionScopeVerified || evidence.TenantId != tenantId ||
            evidence.LegalEntityId != legalEntityId || evidence.RevisionId != revisionId ||
            evidence.AuthorizedSeries is null || evidence.AuthorizedSeries.Count == 0 ||
            evidence.AuthorizedSeries.Distinct().Count() != evidence.AuthorizedSeries.Count)
            return new(SnapshotReadOutcome.NotFound);
        (PublishedRevisionManifest Manifest, PublishedRevisionPart[] Parts)? snapshot;
        try
        {
            snapshot = await store.ReadSnapshotAsync(tenantId, legalEntityId,
                revisionId, cancellationToken);
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
        if (manifest.State != DemandRevisionState.Published)
            return new(SnapshotReadOutcome.StateDenied);
        T? value;
        try
        {
            value = project(manifest, parts);
        }
        catch (InvalidDataException)
        {
            return new(SnapshotReadOutcome.InvalidSnapshot);
        }
        return value is null
            ? new(SnapshotReadOutcome.InvalidCursor)
            : new(SnapshotReadOutcome.Found, value);
    }

    private InternalSnapshotPage? BuildPage(PublishedRevisionManifest manifest,
        PublishedRevisionPart[] parts, int pageSize, string? cursor)
    {
        var rows = parts.OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(x => x.WarehouseId, StringComparer.Ordinal)
            .SelectMany(part => part.Rows.OrderBy(x => x.Number).Select(row =>
                new InternalSnapshotRow(part.SkuId, part.WarehouseId, part.BaseUomId,
                    row.Number, row.WeekStart, row.WeekEnd, row.ValueKind,
                    row.Quantity, row.Source, row.ManualReason))).ToArray();
        if (rows.Length != manifest.ExpectedRowCount)
            throw new InvalidDataException("Published revision row count is invalid.");
        var position = 0;
        if (!string.IsNullOrEmpty(cursor))
        {
            var decoded = Decode(cursor);
            if (decoded is null || decoded.Version != CursorVersion ||
                decoded.TenantId != manifest.TenantId ||
                decoded.LegalEntityId != manifest.LegalEntityId ||
                decoded.RevisionId != manifest.RevisionId ||
                decoded.Checksum != manifest.Checksum ||
                decoded.ContentVersion != manifest.ContentVersion ||
                decoded.StateVersion != manifest.StateVersion ||
                decoded.PageSize != pageSize ||
                decoded.Position is <= 0 || decoded.Position >= rows.Length ||
                decoded.PreviousRowKey != RowKey(rows[decoded.Position - 1]) ||
                decoded.ExpiresAt <= clock.GetUtcNow())
                return null;
            position = decoded.Position;
        }
        var pageRows = rows.Skip(position).Take(pageSize).ToArray();
        var next = position + pageRows.Length;
        var nextCursor = next < rows.Length
            ? Encode(new CursorPayload(CursorVersion, manifest.TenantId,
                manifest.LegalEntityId, manifest.RevisionId, manifest.Checksum,
                manifest.ContentVersion, manifest.StateVersion, pageSize, next,
                RowKey(rows[next - 1]), clock.GetUtcNow().Add(CursorLifetime)))
            : null;
        return new InternalSnapshotPage(Status(manifest), pageRows, nextCursor);
    }

    private static InternalSnapshotStatus Status(PublishedRevisionManifest manifest) =>
        new(manifest.TenantId, manifest.LegalEntityId, manifest.RevisionId,
            manifest.PlanningCycleId, manifest.PlanningPeriodKey, manifest.State,
            manifest.StateVersion, manifest.ContentVersion, manifest.ChecksumScheme,
            manifest.Checksum, manifest.ExpectedPartCount, manifest.ExpectedRowCount,
            manifest.AsOfDate, manifest.CalendarId, manifest.CalendarVersion,
            manifest.TimeZoneId, manifest.HorizonStart, manifest.HorizonEnd,
            manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId)).ToArray());

    private static string RowKey(InternalSnapshotRow row) =>
        $"{row.SkuId:D}|{row.WarehouseId}|{row.WeekNumber:D2}";

    private string Encode(CursorPayload value)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(value);
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
            var suppliedSignature = FromBase64Url(segments[1]);
            var expectedSignature = HMACSHA256.HashData(_signingKey!, data);
            if (suppliedSignature.Length != expectedSignature.Length ||
                !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
                return null;
            return JsonSerializer.Deserialize<CursorPayload>(data);
        }
        catch (Exception error) when (error is FormatException or JsonException)
        {
            return null;
        }
    }

    private static byte[]? GetSigningKey(IConfiguration configuration)
    {
        var value = configuration["DemandPlanning:SnapshotCursorSigningKey"];
        return string.IsNullOrEmpty(value) || Encoding.UTF8.GetByteCount(value) < 32
            ? null : Encoding.UTF8.GetBytes(value);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }

    private sealed record CursorPayload(string Version, Guid TenantId,
        Guid LegalEntityId, Guid RevisionId, string Checksum,
        int ContentVersion, int StateVersion, int PageSize, int Position,
        string PreviousRowKey, DateTimeOffset ExpiresAt);
}

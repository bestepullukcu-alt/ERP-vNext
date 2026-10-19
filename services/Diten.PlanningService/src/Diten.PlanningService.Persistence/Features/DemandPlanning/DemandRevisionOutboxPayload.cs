using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// Owned Demand v2 DRAFT producer shape. Neither the event schema nor its transport is frozen.
internal static class DemandRevisionOutboxPayload
{
    private const string DraftEventVersion = "2.0.0-draft";
    // Length-prefixed UTF-8 fields, Int32 little-endian, lower-case D GUIDs;
    // selected then excluded SKU x Warehouse pairs, each sorted by GUID and WarehouseId.
    private const string ScopeDefinitionId = "MOD0188-SCOPE-1/SHA-256";

    public static string Published(PublishedRevisionManifest manifest, Guid eventId,
        DateTimeOffset occurredAt) => JsonSerializer.Serialize(new
        {
            eventId,
            eventVersion = DraftEventVersion,
            eventType = "DemandRevisionPublished",
            revisionId = manifest.RevisionId,
            tenantId = manifest.TenantId,
            legalEntityId = manifest.LegalEntityId,
            planningCycleId = manifest.PlanningCycleId,
            planningPeriodKey = manifest.PlanningPeriodKey,
            newState = "Published",
            stateVersion = manifest.StateVersion,
            occurredAt,
            scopeSummary = ScopeSummary(manifest),
            integritySummary = IntegritySummary(manifest)
        });

    public static string Invalidated(PublishedRevisionManifest manifest, Guid eventId,
        DateTimeOffset occurredAt, InvalidationImpactCode impactCode, string reason,
        string businessImpact, string evidenceReference, Guid actorId) =>
        JsonSerializer.Serialize(new
        {
            eventId,
            eventVersion = DraftEventVersion,
            eventType = "DemandRevisionInvalidated",
            revisionId = manifest.RevisionId,
            tenantId = manifest.TenantId,
            legalEntityId = manifest.LegalEntityId,
            planningCycleId = manifest.PlanningCycleId,
            planningPeriodKey = manifest.PlanningPeriodKey,
            newState = "Invalidated",
            stateVersion = manifest.StateVersion,
            occurredAt,
            scopeSummary = ScopeSummary(manifest),
            integritySummary = IntegritySummary(manifest),
            invalidation = new
            {
                reasonCategory = ReasonCategory(impactCode),
                reason,
                businessImpact,
                evidenceReference,
                actorId,
                occurredAt
            }
        });

    private static object ScopeSummary(PublishedRevisionManifest manifest) => new
    {
        selectedSeriesCount = manifest.SelectedSeries.Count,
        excludedSeriesCount = manifest.ExcludedSeries.Count,
        scopeDefinitionId = ScopeDefinitionId,
        scopeDigest = ScopeDigest(manifest)
    };

    private static object IntegritySummary(PublishedRevisionManifest manifest) => new
    {
        expectedPartCount = manifest.ExpectedPartCount,
        expectedRowCount = manifest.ExpectedRowCount,
        definitionId = manifest.ChecksumScheme,
        digest = manifest.Checksum,
        verificationState = "Verified"
    };

    private static string ReasonCategory(InvalidationImpactCode code) => code switch
    {
        InvalidationImpactCode.IdentityOrScope => "IdentityOrScope",
        InvalidationImpactCode.CalendarTimeUnitOrQuantity => "CalendarUnitOrQuantity",
        InvalidationImpactCode.SourceMethodOrPolicy => "SourceOrMethod",
        InvalidationImpactCode.ContentIntegrity => "MissingDuplicateOrCorruptContent",
        _ => throw new InvalidDataException("Invalidation has no material-impact category.")
    };

    private static string ScopeDigest(PublishedRevisionManifest manifest)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        void Field(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        Field("MOD0188-SCOPE-1");
        foreach (var selected in manifest.SelectedSeries
                     .OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
                     .ThenBy(x => x.WarehouseId, StringComparer.Ordinal))
        {
            Field("selected"); Field(selected.SkuId.ToString("D")); Field(selected.WarehouseId);
        }
        foreach (var excluded in manifest.ExcludedSeries
                     .OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
                     .ThenBy(x => x.WarehouseId, StringComparer.Ordinal))
        {
            Field("excluded"); Field(excluded.SkuId.ToString("D")); Field(excluded.WarehouseId);
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
}

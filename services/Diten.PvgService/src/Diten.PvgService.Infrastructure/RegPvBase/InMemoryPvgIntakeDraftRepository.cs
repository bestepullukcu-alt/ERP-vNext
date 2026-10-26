using System.Collections.Concurrent;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Infrastructure.RegPvBase;

public sealed class InMemoryPvgIntakeDraftRepository : IPvgIntakeDraftStore
{
    private const long InitialVersion = 1;

    private readonly ConcurrentDictionary<PvgIntakeDraftKey, PvgIntakeDraftEntity> _drafts = new();

    public ValueTask<string> AddAsync(
        PvgPersistenceTenantScope tenantScope,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(tenantScope, intake);

        var intakeDraftId = NewDraftId();
        _drafts[new PvgIntakeDraftKey(tenantScope.TenantId, intakeDraftId)] =
            PvgIntakeDraftEntity.FromDomain(tenantScope.TenantId, intakeDraftId, InitialVersion, intake);

        return ValueTask.FromResult(intakeDraftId);
    }

    public ValueTask<PvgIntakeDraftReplaceResult> ReplaceAsync(
        PvgPersistenceTenantScope tenantScope,
        string intakeDraftId,
        long? expectedVersion,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(tenantScope, intake);

        if (string.IsNullOrWhiteSpace(intakeDraftId))
        {
            return ValueTask.FromResult(PvgIntakeDraftReplaceResult.NotFound());
        }

        var key = new PvgIntakeDraftKey(tenantScope.TenantId, intakeDraftId);
        if (!_drafts.TryGetValue(key, out var current))
        {
            return ValueTask.FromResult(PvgIntakeDraftReplaceResult.NotFound());
        }

        if (expectedVersion is null || current.Version != expectedVersion.Value)
        {
            return ValueTask.FromResult(PvgIntakeDraftReplaceResult.VersionConflict());
        }

        var replacement = PvgIntakeDraftEntity.FromDomain(tenantScope.TenantId, intakeDraftId, current.Version + 1, intake);
        return ValueTask.FromResult(
            _drafts.TryUpdate(key, replacement, current)
                ? PvgIntakeDraftReplaceResult.Replaced(replacement.Version)
                : PvgIntakeDraftReplaceResult.VersionConflict());
    }

    public ValueTask<PvgPersistedIntakeDraft?> FindByIdAsync(
        PvgPersistenceTenantScope tenantScope,
        string intakeDraftId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(tenantScope);

        if (string.IsNullOrWhiteSpace(intakeDraftId))
        {
            return ValueTask.FromResult<PvgPersistedIntakeDraft?>(null);
        }

        return _drafts.TryGetValue(new PvgIntakeDraftKey(tenantScope.TenantId, intakeDraftId), out var entity)
            ? ValueTask.FromResult<PvgPersistedIntakeDraft?>(entity.ToPersistedDraft())
            : ValueTask.FromResult<PvgPersistedIntakeDraft?>(null);
    }

    public ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> ListAsync(
        PvgPersistenceListScope listScope,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(listScope.TenantScope);

        var pageNumber = Math.Max(1, listScope.PageNumber);
        var pageSize = Math.Max(1, listScope.PageSize);
        var items = _drafts
            .Values
            .Where(entity => entity.TenantId == listScope.TenantScope.TenantId)
            .Where(entity => listScope.Status is null || entity.Status == listScope.Status)
            .OrderByDescending(entity => entity.ReceivedAtUtc)
            .ThenBy(entity => entity.IntakeDraftId, StringComparer.Ordinal)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(entity => entity.ToPersistedDraft())
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<PvgPersistedIntakeDraft>>(items);
    }

    private static void EnsureTenantScope(PvgPersistenceTenantScope tenantScope, SafetyCaseIntake? intake = null)
    {
        if (string.IsNullOrWhiteSpace(tenantScope.TenantId))
        {
            throw new InvalidOperationException("PVG_PERSISTENCE_TENANT_SCOPE_REQUIRED");
        }

        if (intake is not null && !string.Equals(intake.TenantId, tenantScope.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PVG_PERSISTENCE_TENANT_SCOPE_MISMATCH");
        }
    }

    private static string NewDraftId() => $"pvg-draft-{Guid.NewGuid():N}";

    private sealed record PvgIntakeDraftKey(string TenantId, string IntakeDraftId);

    private sealed record PvgIntakeDraftEntity(
        string TenantId,
        string IntakeDraftId,
        long Version,
        PvgIntakeStatus Status,
        string IntakeChannel,
        string SourceType,
        string? SourceReference,
        DateTimeOffset ReceivedAtUtc,
        string ReporterType,
        string? ReporterContactSummary,
        string? PatientSubjectCode,
        DateOnly? EventOnsetDate,
        string AdverseEventNarrative,
        string Seriousness,
        string IntakePriority,
        PvgTriageOutcome? TriageOutcome,
        string? TriageReason,
        string? RouteTargetQueue,
        IReadOnlyList<string> EvidenceLinkReferences,
        IReadOnlyList<PvgIntakeProductLine> ProductLines,
        DateTimeOffset AwarenessAtUtc,
        string? CompanyRecipientPersonId,
        string EventCountry,
        string ReportType,
        PvgReporterSource ReporterSource,
        string? ReporterSourceId,
        PvgReporterCrmSearchState ReporterCrmSearchState,
        string? ReporterNameOrInitials,
        string? ReporterProfession,
        string? ReporterCountry)
    {
        public static PvgIntakeDraftEntity FromDomain(
            string tenantId,
            string intakeDraftId,
            long version,
            SafetyCaseIntake intake) =>
            new(
                tenantId,
                intakeDraftId,
                version,
                intake.Status,
                intake.IntakeChannel,
                intake.SourceType,
                intake.SourceReference,
                intake.ReceivedAtUtc,
                intake.ReporterType,
                intake.ReporterContactSummary,
                intake.PatientSubjectCode,
                intake.EventOnsetDate,
                intake.AdverseEventNarrative,
                intake.Seriousness,
                intake.IntakePriority,
                intake.TriageOutcome,
                intake.TriageReason,
                intake.RouteTargetQueue,
                intake.EvidenceLinkReferences.ToArray(),
                intake.ProductLines.ToArray(),
                intake.AwarenessAtUtc,
                intake.CompanyRecipientPersonId,
                intake.EventCountry,
                intake.ReportType,
                intake.ReporterSource,
                intake.ReporterSourceId,
                intake.ReporterCrmSearchState,
                intake.ReporterNameOrInitials,
                intake.ReporterProfession,
                intake.ReporterCountry);

        public PvgPersistedIntakeDraft ToPersistedDraft() =>
            new(
                IntakeDraftId,
                new SafetyCaseIntake(
                    TenantId,
                    Status,
                    IntakeChannel,
                    SourceType,
                    ReceivedAtUtc,
                    ReporterType,
                    AdverseEventNarrative,
                    Seriousness,
                    IntakePriority,
                    TriageOutcome,
                    TriageReason,
                    RouteTargetQueue)
                {
                    SourceReference = SourceReference,
                    ReporterContactSummary = ReporterContactSummary,
                    PatientSubjectCode = PatientSubjectCode,
                    EventOnsetDate = EventOnsetDate,
                    EvidenceLinkReferences = EvidenceLinkReferences.ToArray(),
                    ProductLines = ProductLines.ToArray(),
                    AwarenessAtUtc = AwarenessAtUtc,
                    CompanyRecipientPersonId = CompanyRecipientPersonId,
                    EventCountry = EventCountry,
                    ReportType = ReportType,
                    ReporterSource = ReporterSource,
                    ReporterSourceId = ReporterSourceId,
                    ReporterCrmSearchState = ReporterCrmSearchState,
                    ReporterNameOrInitials = ReporterNameOrInitials,
                    ReporterProfession = ReporterProfession,
                    ReporterCountry = ReporterCountry
                },
                Version);
    }
}

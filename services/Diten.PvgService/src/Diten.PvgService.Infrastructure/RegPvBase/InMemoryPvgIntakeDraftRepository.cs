using System.Collections.Concurrent;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Infrastructure.RegPvBase;

public sealed class InMemoryPvgIntakeDraftRepository : IPvgIntakeDraftStore
{
    private readonly ConcurrentDictionary<PvgIntakeDraftKey, PvgIntakeDraftEntity> _drafts = new();

    public ValueTask<string> AddAsync(
        PvgPersistenceTenantScope tenantScope,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(tenantScope, intake);

        var intakeDraftId = NewDraftId();
        _drafts[new PvgIntakeDraftKey(tenantScope.TenantId, intakeDraftId)] =
            PvgIntakeDraftEntity.FromDomain(tenantScope.TenantId, intakeDraftId, intake);

        return ValueTask.FromResult(intakeDraftId);
    }

    public ValueTask<bool> ReplaceAsync(
        PvgPersistenceTenantScope tenantScope,
        string intakeDraftId,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(tenantScope, intake);

        if (string.IsNullOrWhiteSpace(intakeDraftId))
        {
            return ValueTask.FromResult(false);
        }

        var key = new PvgIntakeDraftKey(tenantScope.TenantId, intakeDraftId);
        if (!_drafts.ContainsKey(key))
        {
            return ValueTask.FromResult(false);
        }

        _drafts[key] = PvgIntakeDraftEntity.FromDomain(tenantScope.TenantId, intakeDraftId, intake);
        return ValueTask.FromResult(true);
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
        string? SuspectProductText,
        string Seriousness,
        string IntakePriority,
        PvgTriageOutcome? TriageOutcome,
        string? TriageReason,
        string? RouteTargetQueue,
        IReadOnlyList<string> EvidenceLinkReferences)
    {
        public static PvgIntakeDraftEntity FromDomain(
            string tenantId,
            string intakeDraftId,
            SafetyCaseIntake intake) =>
            new(
                tenantId,
                intakeDraftId,
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
                intake.SuspectProductText,
                intake.Seriousness,
                intake.IntakePriority,
                intake.TriageOutcome,
                intake.TriageReason,
                intake.RouteTargetQueue,
                intake.EvidenceLinkReferences.ToArray());

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
                    SuspectProductText = SuspectProductText,
                    EvidenceLinkReferences = EvidenceLinkReferences.ToArray()
                });
    }
}

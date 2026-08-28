using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU14 — External Document Register repository contracts. Every method is tenant-scoped via the
// TenantRepository ExecutionFilter; nothing is ever hard-deleted (supersession/archival/closing are status changes).

/// <summary>MOD-0029-FU14 — tenant-scoped list filter for the external document register.</summary>
public sealed record ExternalDocumentListFilter(
    ExternalDocumentStatus? ExternalDocumentStatus = null,
    ExternalSourceStatus? SourceStatus = null,
    ExternalDocumentType? ExternalDocumentType = null,
    ExternalImpactAssessmentStatus? ImpactAssessmentStatus = null,
    Guid? MonitoringOwnerUserId = null);

public interface IExternalDocumentRegisterRepository
{
    Task<ExternalDocumentRegisterEntry> CreateAsync(ExternalDocumentRegisterEntry entry, CancellationToken ct = default);
    Task<ExternalDocumentRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalDocumentRegisterEntry>> ListAsync(ExternalDocumentListFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalDocumentRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(ExternalDocumentRegisterEntry entry, CancellationToken ct = default);
}

public interface IExternalDocumentMonitoringCheckRepository
{
    Task<ExternalDocumentMonitoringCheck> CreateAsync(ExternalDocumentMonitoringCheck check, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalDocumentMonitoringCheck>> GetByExternalDocumentAsync(Guid externalDocumentRegisterEntryId, CancellationToken ct = default);
}

public interface IExternalDocumentImpactAssessmentRepository
{
    Task<ExternalDocumentImpactAssessment> CreateAsync(ExternalDocumentImpactAssessment assessment, CancellationToken ct = default);
    Task<ExternalDocumentImpactAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetByExternalDocumentAsync(Guid externalDocumentRegisterEntryId, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(ExternalDocumentImpactAssessment assessment, CancellationToken ct = default);
}

public interface IExternalDocumentInternalLinkRepository
{
    Task<ExternalDocumentInternalLink> CreateAsync(ExternalDocumentInternalLink link, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalDocumentInternalLink>> GetByExternalDocumentAsync(Guid externalDocumentRegisterEntryId, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalDocumentInternalLink>> GetByInternalRegisterEntryAsync(Guid internalRegisterEntryId, CancellationToken ct = default);
    Task<bool> UpdateAsync(ExternalDocumentInternalLink link, CancellationToken ct = default);
}

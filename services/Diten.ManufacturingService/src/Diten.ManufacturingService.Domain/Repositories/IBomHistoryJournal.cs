using Diten.ManufacturingService.Domain.Entities;

namespace Diten.ManufacturingService.Domain.Repositories;

/// <summary>
/// BOM yazımı + sürüm geçmişi — AUD-001 yol c izi <c>bom-surum-gecmisi</c>, belirteç <c>IBomHistoryJournal.CommitAsync</c>.
/// <see cref="CommitAsync"/> değişen BOM sürümlerini VE geçmiş kayıtlarını tek bir Mongo işleminde yazar; biri
/// yazılamazsa hiçbiri yazılmaz (GxP, K2 fail-closed). Geçmiş için güncelleme / silme üyesi yoktur (AUD-001 §4.1).
/// </summary>
public interface IBomHistoryJournal
{
    Task<BomCommitResult> CommitAsync(BomChangeSet changeSet, CancellationToken ct);

    Task<IReadOnlyList<BomHistoryEntry>> ListAsync(Guid tenantId, Guid legalEntityId, Guid bomVersionId, CancellationToken ct);
}

/// <summary>
/// Tek işlemde yazılacak değişiklik. <see cref="Inserted"/> yeni sürüm; <see cref="Updated"/> mevcut sürümlerin yeni
/// hali ve her birinin beklenen eşzamanlılık belirteci (yazımdan ÖNCEKİ <c>Version</c>).
/// </summary>
public sealed record BomChangeSet(
    Guid TenantId,
    Guid LegalEntityId,
    BomVersion? Inserted,
    IReadOnlyList<(BomVersion Entity, int ExpectedVersion)> Updated,
    IReadOnlyList<BomHistoryEntry> History);

public enum BomCommitResult
{
    Committed,
    /// <summary>Beklenen belirteç tutmadı ya da eşsizlik (tek Effective / revizyon no) ihlali — eşzamanlı yazım.</summary>
    Conflict
}

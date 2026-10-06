using Diten.ManufacturingService.Domain.Entities;

namespace Diten.ManufacturingService.Domain.Repositories;

/// <summary>
/// BOM okuma deposu. Her metot Tenant + LegalEntity ile sınırlanır ve silinmiş kaydı görmez; başka kiracının/LE'nin
/// kaydı "yok" (null / boş) döner — çağıran 404 üretir. Yazma bu arayüzde YOK: her yazma geçmişle birlikte
/// <see cref="IBomHistoryJournal.CommitAsync"/> üzerinden tek işlemde yapılır.
/// </summary>
public interface IBomRepository
{
    Task<BomVersion?> GetByIdAsync(Guid tenantId, Guid legalEntityId, Guid bomVersionId, CancellationToken ct);

    /// <summary>Verilen anda yürürlükte olan sürüm (Effective ya da o tarihte geçerli Superseded).</summary>
    Task<BomVersion?> GetEffectiveAtAsync(Guid tenantId, Guid legalEntityId, Guid itemId, DateTimeOffset at, CancellationToken ct);

    /// <summary>Item'ın şu an Effective sürümü (en fazla bir tane — unique kısmi index).</summary>
    Task<BomVersion?> GetCurrentEffectiveAsync(Guid tenantId, Guid legalEntityId, Guid itemId, CancellationToken ct);

    /// <summary>Verilen item'ların şu an Effective sürümleri — döngü taraması için.</summary>
    Task<IReadOnlyList<BomVersion>> GetCurrentEffectiveForItemsAsync(Guid tenantId, Guid legalEntityId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct);

    /// <summary>Item için sıradaki revizyon numarası (silinmişler dahil max + 1).</summary>
    Task<int> GetNextRevisionNoAsync(Guid tenantId, Guid legalEntityId, Guid itemId, CancellationToken ct);

    /// <summary>
    /// Platform server-mode liste sözleşmesi (BL-440): <c>Total</c> = kapsamdaki tüm sürümler, <c>FilteredTotal</c> =
    /// filtre + aramaya uyanlar, <c>Items</c> = istenen dilim. <see cref="BomListFilter.Take"/> null ise sınır yok
    /// (export, üst sınırı çağıran koyar).
    /// </summary>
    Task<BomListPage> ListAsync(Guid tenantId, Guid legalEntityId, BomListFilter filter, CancellationToken ct);
}

public sealed record BomListFilter(
    Guid? ItemId,
    IReadOnlyCollection<BomStatus> Statuses,
    string? Search,
    BomListOrder OrderBy,
    bool Descending,
    int Skip,
    int? Take);

public enum BomListOrder
{
    UpdatedAt,
    ItemId,
    Version,
    Status,
    Description,
    EffectiveFrom
}

public sealed record BomListPage(IReadOnlyList<BomVersion> Items, long Total, long FilteredTotal);

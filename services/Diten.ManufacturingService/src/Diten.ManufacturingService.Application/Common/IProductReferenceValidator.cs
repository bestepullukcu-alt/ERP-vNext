namespace Diten.ManufacturingService.Application.Common;

/// <summary>
/// PRODUCT-MASTER (MOD-0290) tüketim seam'i — ana ürün ve bileşen kimlikleri başka bir servisin (MDM) kimliğidir; BOM
/// onları yaratmaz, yalnız var olduklarını doğrular (fail-closed → 422 <c>UNKNOWN_ITEM</c>). Frozen contract:
/// <c>product-master-bundle.openapi.yaml</c> <c>POST /validate</c>. Kayıt <c>ProductMaster:Mode</c> ayarıyla seçilir
/// (pack §7): <c>Permissive</c> (varsayılan — 0290 validate ucu henüz canlı değil) ya da <c>Http</c>.
/// </summary>
public interface IProductReferenceValidator
{
    /// <summary>Verilen item kimliklerinden MOD-0290'da BİLİNMEYENLERİ döner. Boş liste → hepsi bilinir.</summary>
    Task<IReadOnlyList<Guid>> GetUnknownItemIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct);
}

/// <summary>
/// Progressive-integration varsayılanı: MOD-0290 <c>validate</c> ucu bu dilimde canlı değil → her kimliği bilinir
/// kabul eder. Gerçek uç açılınca yalnız <c>ProductMaster:Mode=Http</c> yapılır; BOM kodu değişmez (F-0193-07).
/// </summary>
public sealed class PermissiveProductReferenceValidator : IProductReferenceValidator
{
    public Task<IReadOnlyList<Guid>> GetUnknownItemIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
}

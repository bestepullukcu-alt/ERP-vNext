namespace Diten.ProcurementService.Application.Common;

/// <summary>
/// PRODUCT-MASTER (MOD-0290) consume seam'i. itemId/uomId BAŞKA bir servisin (MDM) kimliğidir; bu modül onları
/// yaratmaz — yalnız var-olma doğrulaması yapar (fail-closed → 404 UNKNOWN_REFERENCE). MOD-0290 ayrı servistir ve
/// bu dilimde çalışmıyor: entegrasyon progressive (mock/real later). Varsayılan üretim implementasyonu 0290 yokken
/// hard-fail ETMEZ (permissive) — böylece servis 0290 olmadan da ayakta kalır; birim testlerde fake ile bilinmeyen
/// referans → 404 yolu doğrulanır.
/// </summary>
public interface IProductReferenceValidator
{
    /// <summary>
    /// Verilen itemId'ler içinde MOD-0290'da BİLİNMEYEN olanları döner (fail-closed). Boş liste → hepsi bilinir.
    /// </summary>
    Task<IReadOnlyList<string>> GetUnknownItemIdsAsync(IReadOnlyList<string> itemIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Varsayılan (progressive-integration) implementasyon: MOD-0290 bu dilimde bağlı değil → hard-fail etmez, tüm
/// itemId'leri bilinir kabul eder (permissive). Gerçek 0290 gateway'i bağlanınca bu kayıt değiştirilir (mock/real).
/// </summary>
public sealed class PermissiveProductReferenceValidator : IProductReferenceValidator
{
    public Task<IReadOnlyList<string>> GetUnknownItemIdsAsync(IReadOnlyList<string> itemIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}

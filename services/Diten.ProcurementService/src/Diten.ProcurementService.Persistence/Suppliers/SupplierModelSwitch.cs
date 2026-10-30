using Microsoft.Extensions.Configuration;

namespace Diten.ProcurementService.Persistence.Suppliers;

/// <summary>Hangi tedarikçi modelinin okunduğu (MOD-0140 §8 "Göç", öneri §2.3).</summary>
public enum SupplierModel
{
    /// <summary>Bugünkü şirket bazlı kayıt (<c>suppliers</c>). Varsayılan ve geri alma değeri.</summary>
    PerLegalEntity = 0,

    /// <summary>Kiracıda tek kayıt + şirket ilişkisi (<c>supplier_masters</c>, <c>supplier_company_relationships</c>).</summary>
    TenantShared = 1
}

/// <summary>
/// Okuma anahtarı <c>Suppliers:Model</c> (DL-2). Göç ve <c>verify</c> geçince <see cref="SupplierModel.TenantShared"/>
/// açılır; geri alma anahtarı <see cref="SupplierModel.PerLegalEntity"/>'ye döndürmektir — eski koleksiyon göçte değişmediği
/// için eski davranış anında döner. Anahtarı okuyan yol DL-3'tedir; bu dilim yalnız değeri tanımlar ve doğrular.
/// </summary>
public sealed record SupplierModelSwitch(SupplierModel Model)
{
    public const string ConfigurationKey = "Suppliers:Model";

    /// <summary>Anahtar yoksa <see cref="SupplierModel.PerLegalEntity"/>; tanınmayan değer açılışı durdurur (sessiz varsayılan yok).</summary>
    public static SupplierModelSwitch FromConfiguration(IConfiguration configuration)
    {
        var raw = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new SupplierModelSwitch(SupplierModel.PerLegalEntity);
        }

        var value = raw.Trim();
        foreach (var model in Enum.GetValues<SupplierModel>())
        {
            if (string.Equals(model.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                return new SupplierModelSwitch(model);
            }
        }

        throw new InvalidOperationException(
            $"Configuration error: '{ConfigurationKey}' must be '{nameof(SupplierModel.PerLegalEntity)}' or " +
            $"'{nameof(SupplierModel.TenantShared)}' (got '{value}').");
    }
}

namespace Diten.ProcurementService.Application.Common;

/// <summary>
/// 3-yönlü eşleştirme tolerans profili (ASSUMPTION-P2P-01). qty/price/amount alanları için MUTLAK sapma eşikleri
/// (Decimal). Eşikler POLICY-DRIVEN'dır: değerleri tenant policy / Finance kararıdır (EA/Finance-TBD) ve
/// <see cref="IMatchTolerancePolicy"/> seam'i üzerinden çözülür — match mantığına GÖMÜLMEZ. Bir alanın sapması
/// eşiğe eşit veya altında ise <c>withinTolerance</c>; üstünde ise exception.
/// </summary>
public sealed record MatchToleranceProfile(
    string ProfileId,
    decimal QuantityTolerance,
    decimal PriceTolerance,
    decimal AmountTolerance);

/// <summary>
/// TOLERANCE POLICY seam (ASSUMPTION-P2P-01). 3-yönlü eşleştirme toleransı POLICY-DRIVEN modellenir: bir
/// <c>toleranceProfileId</c> → qty/price/amount eşiklerine çözülür. runThreeWayMatch mantığı bu seam'i ÇAĞIRIR;
/// hiçbir sabit sayısal tolerans match koduna/contract'a GÖMÜLMEZ. <paramref name="toleranceProfileId"/> null ise
/// LE/tenant default profil bu seam'de çözülür (contract sayı gömmez). Bu, IProductReferenceValidator /
/// IInventoryPostingClient seam deseninin aynısıdır (interface + varsayılan implementasyon; DI'da register edilir).
/// Gerçek tolerans değeri/sahibi Finance/EA-TBD (0143-FU); seam bağlanınca match kodu DEĞİŞMEZ.
/// </summary>
public interface IMatchTolerancePolicy
{
    Task<MatchToleranceProfile> ResolveAsync(string? toleranceProfileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Varsayılan (progressive-integration) implementasyon: gerçek tolerans policy servisi (Finance/EA-TBD) bu dilimde
/// bağlı DEĞİL. ZERO-TOLERANCE profil döner — qty/price/amount eşikleri 0. Sayısal değer (0) BU SEAM'DE yaşar,
/// match mantığında DEĞİL (ASSUMPTION-P2P-01); herhangi bir sapma → exception (fail-closed, sessiz geçiş yok).
/// Gerçek policy seam'i bağlanınca yalnız bu kayıt değiştirilir; runThreeWayMatch kodu DEĞİŞMEZ (contract sınırı).
/// </summary>
public sealed class ZeroToleranceMatchPolicy : IMatchTolerancePolicy
{
    public Task<MatchToleranceProfile> ResolveAsync(string? toleranceProfileId, CancellationToken cancellationToken = default)
        => Task.FromResult(new MatchToleranceProfile(
            ProfileId: string.IsNullOrWhiteSpace(toleranceProfileId) ? "DEFAULT" : toleranceProfileId!.Trim(),
            QuantityTolerance: 0m,
            PriceTolerance: 0m,
            AmountTolerance: 0m));
}

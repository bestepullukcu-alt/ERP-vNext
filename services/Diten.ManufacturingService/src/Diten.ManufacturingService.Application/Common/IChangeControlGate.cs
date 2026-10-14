namespace Diten.ManufacturingService.Application.Common;

/// <summary>
/// MOD-0209 Change Control seam'i — bir BOM sürümü yalnız kabul edilmiş bir değişiklik kontrolü referansıyla yürürlüğe
/// girer. MOD-0209 repoda yok (waiver W-0193-01): varsayılan <see cref="FormatOnlyChangeControlGate"/> yalnız biçimi
/// doğrular. Gerçek 0209 geldiğinde bu kayıt değişir; release kodu değişmez (F-0193-04).
/// </summary>
public interface IChangeControlGate
{
    Task<bool> IsAcceptedAsync(Guid tenantId, Guid legalEntityId, string changeControlRef, CancellationToken ct);
}

public sealed class FormatOnlyChangeControlGate : IChangeControlGate
{
    public const int MaxLength = 64;

    public Task<bool> IsAcceptedAsync(Guid tenantId, Guid legalEntityId, string changeControlRef, CancellationToken ct) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(changeControlRef)
            && changeControlRef.Length <= MaxLength
            && changeControlRef.Trim() == changeControlRef);
}

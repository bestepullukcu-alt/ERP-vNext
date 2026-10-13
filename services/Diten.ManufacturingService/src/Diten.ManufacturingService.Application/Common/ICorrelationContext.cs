namespace Diten.ManufacturingService.Application.Common;

/// <summary>
/// İstek korelasyon kimliği (Blueprint MOD-0040 Canonical ID &amp; Correlation standardı). Api katmanındaki
/// korelasyon ara katmanı doldurur: geçerli bir UUID <c>X-Correlation-Id</c> geldiyse o, yoksa sunucu üretir.
/// Her hata gövdesi ve her geçmiş kaydı bu değeri taşır.
/// </summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }
    void Set(string correlationId);
}

public sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; private set; } = Guid.NewGuid().ToString();

    public void Set(string correlationId) => CorrelationId = correlationId;
}

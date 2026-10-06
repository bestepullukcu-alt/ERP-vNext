namespace Diten.ManufacturingService.Application.Common;

public static class TimeProviderExtensions
{
    /// <summary>
    /// UTC "şimdi", milisaniyeye kırpılmış: BSON tarihleri milisaniye saklar. Yanıtta dönen zaman ile sonra okunan
    /// zaman aynı olsun, ve yürürlük sınırı (<c>effectiveTo</c> = yeni <c>effectiveFrom</c>) kayıttan sonra da birebir eşleşsin.
    /// </summary>
    public static DateTimeOffset UtcNowMs(this TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        return new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
    }
}

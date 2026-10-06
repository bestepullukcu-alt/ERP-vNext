using Diten.ManufacturingService.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Diten.ManufacturingService.Infrastructure.Middleware;

/// <summary>
/// Blueprint MOD-0040 korelasyon kuralı: tek bir UUID <c>X-Correlation-Id</c> geldiyse o kullanılır, yoksa sunucu
/// üretir; değer yanıt başlığında döner ve her hata gövdesine / geçmiş kaydına yazılır. Kimlik doğrulamadan ÖNCE
/// çalışır ki 401/400 gövdeleri de korelasyon taşısın.
/// </summary>
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlation)
    {
        var values = context.Request.Headers[Header];
        var id = values.Count == 1 && Guid.TryParseExact(values[0], "D", out var parsed) && parsed != Guid.Empty
            ? parsed.ToString()
            : Guid.NewGuid().ToString();
        correlation.Set(id);
        context.Response.Headers[Header] = id;
        await next(context);
    }
}

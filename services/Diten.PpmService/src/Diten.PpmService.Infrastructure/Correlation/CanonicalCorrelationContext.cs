using Diten.PpmService.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Diten.PpmService.Infrastructure.Correlation;

public sealed class CanonicalCorrelationContext(IHttpContextAccessor httpContextAccessor)
    : ICorrelationContext
{
    public const string HeaderName = "X-Correlation-Id";
    private static readonly object ItemKey = new();
    private Guid? _backgroundCorrelationId;

    public Guid CorrelationId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return _backgroundCorrelationId ??= Guid.NewGuid();
            }

            if (httpContext.Items.TryGetValue(ItemKey, out var existing)
                && existing is Guid existingId
                && existingId != Guid.Empty)
            {
                return existingId;
            }

            var incoming = httpContext.Request.Headers[HeaderName].FirstOrDefault();
            var correlationId = Guid.TryParseExact(incoming, "D", out var parsed)
                                && parsed != Guid.Empty
                ? parsed
                : Guid.NewGuid();
            httpContext.Items[ItemKey] = correlationId;
            return correlationId;
        }
    }
}

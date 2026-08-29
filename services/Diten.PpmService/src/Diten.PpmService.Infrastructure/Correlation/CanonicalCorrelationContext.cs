using Diten.PpmService.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Diten.PpmService.Infrastructure.Correlation;

public sealed class CanonicalCorrelationContext(IHttpContextAccessor httpContextAccessor)
    : ICorrelationContext
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly object _sync = new();
    private Guid? _correlationId;

    public Guid CorrelationId
    {
        get
        {
            lock (_sync)
            {
                if (_correlationId is { } existing)
                {
                    return existing;
                }

                var incoming = httpContextAccessor.HttpContext?
                    .Request.Headers[HeaderName].FirstOrDefault();
                _correlationId = TryParseCanonicalGuid(incoming, out var parsed)
                    ? parsed
                    : Guid.NewGuid();
                return _correlationId.Value;
            }
        }
    }

    private static bool TryParseCanonicalGuid(string? value, out Guid correlationId)
    {
        var parsed = Guid.TryParseExact(value, "D", out correlationId)
                     || Guid.TryParseExact(value, "N", out correlationId);
        return parsed && correlationId != Guid.Empty;
    }
}

using Diten.MdmService.Application.Contracts.Audit;

namespace Diten.MdmService.Api.Audit;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (M2) — the request's correlation id, normalised once per request and kept on the
/// HttpContext, so the controller (history row) and the audit forwarder (central event) read the same Guid.
/// </summary>
public sealed class HttpRequestCorrelation : IRequestCorrelation
{
    private static readonly object ItemKey = new();
    private readonly IHttpContextAccessor _accessor;
    private Guid? _fallback;

    public HttpRequestCorrelation(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid CorrelationId
    {
        get
        {
            var context = _accessor.HttpContext;
            if (context is null)
            {
                return _fallback ??= Guid.NewGuid();
            }

            if (context.Items.TryGetValue(ItemKey, out var stored) && stored is Guid known)
            {
                return known;
            }

            var resolved = RequestCorrelation.Parse(context.Request.Headers[RequestCorrelation.HeaderName].ToString())
                           ?? Guid.NewGuid();
            context.Items[ItemKey] = resolved;
            return resolved;
        }
    }
}

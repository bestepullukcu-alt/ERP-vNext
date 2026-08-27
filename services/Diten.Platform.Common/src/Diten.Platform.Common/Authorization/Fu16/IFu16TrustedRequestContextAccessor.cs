using Microsoft.AspNetCore.Http;

namespace Diten.Platform.Common.Authorization.Fu16;

public interface IFu16TrustedRequestContextAccessor
{
    Fu16TrustedRequestContext? Current { get; }
    void Publish(Fu16TrustedRequestContext context);
}

internal sealed class HttpFu16TrustedRequestContextAccessor(IHttpContextAccessor httpContextAccessor)
    : IFu16TrustedRequestContextAccessor
{
    internal const string ItemKey = "Diten.FU16.TrustedRequestContext";
    public Fu16TrustedRequestContext? Current =>
        httpContextAccessor.HttpContext?.Items[ItemKey] as Fu16TrustedRequestContext;

    public void Publish(Fu16TrustedRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("FU16 trusted context requires an active HTTP request.");
        if (httpContext.Items.ContainsKey(ItemKey))
            throw new InvalidOperationException("FU16 trusted context is immutable once published.");
        httpContext.Items[ItemKey] = context.Validate();
    }
}

using Diten.Web.Services.Branding;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.ViewComponents;

// FIX-TENANT-FAVICON — renders the browser-tab favicon for the tenant shell. Mirrors TenantBrandViewComponent's
// best-effort approach: resolve the current tenant (from the JWT tenant_id) and pull its FaviconDataUrl via the
// shared IBrandingGateway. Any miss/failure yields an empty model, so the view falls back to the default favicon
// and the document head is never broken.
public sealed class TenantFaviconViewComponent : ViewComponent
{
    private readonly IBrandingGateway _brandingGateway;
    private readonly ILogger<TenantFaviconViewComponent> _logger;

    public TenantFaviconViewComponent(IBrandingGateway brandingGateway, ILogger<TenantFaviconViewComponent> logger)
    {
        _brandingGateway = brandingGateway;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await ResolveAsync(HttpContext.RequestAborted);
        return View(model);
    }

    private async Task<TenantFaviconViewModel> ResolveAsync(CancellationToken ct)
    {
        try
        {
            if (GetTenantId() is { } tenantId && tenantId != Guid.Empty)
            {
                var branding = await _brandingGateway.GetTenantBrandingAsync(tenantId, ct);
                if (branding is not null)
                {
                    return new TenantFaviconViewModel(branding.FaviconDataUrl);
                }
            }
        }
        catch (Exception ex)
        {
            // Best-effort: a branding failure must never break the shell head — fall back to the default favicon.
            _logger.LogDebug(ex, "Tenant favicon resolve failed; rendering default favicon.");
        }

        return TenantFaviconViewModel.Default;
    }

    private Guid? GetTenantId()
    {
        var raw = UserClaimsPrincipal?.Claims.FirstOrDefault(c =>
            c.Type == "tenant_id" ||
            c.Type == "tenantId" ||
            c.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

public sealed record TenantFaviconViewModel(string? FaviconDataUrl)
{
    public static readonly TenantFaviconViewModel Default = new((string?)null);

    public bool HasFavicon => !string.IsNullOrWhiteSpace(FaviconDataUrl);
}

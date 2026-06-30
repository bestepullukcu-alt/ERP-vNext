using System.Security.Claims;
using Diten.Web.Services.Branding;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.ViewComponents;

// FIX-SHELL-BRANDING — renders the tenant-shell sidebar brand. Reuses the FIX-LOGIN-BRANDING IBrandingGateway to
// resolve the current tenant's branding (from the JWT tenant_id) server-side. Best-effort: any miss/failure yields
// the default model, so the hardcoded default brand (icon + "Di10") still renders if branding is unavailable.
public sealed class TenantBrandViewComponent : ViewComponent
{
    private readonly IBrandingGateway _brandingGateway;
    private readonly ILogger<TenantBrandViewComponent> _logger;

    public TenantBrandViewComponent(IBrandingGateway brandingGateway, ILogger<TenantBrandViewComponent> logger)
    {
        _brandingGateway = brandingGateway;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await ResolveAsync(HttpContext.RequestAborted);
        return View(model);
    }

    private async Task<TenantBrandViewModel> ResolveAsync(CancellationToken ct)
    {
        try
        {
            if (GetTenantId() is { } tenantId && tenantId != Guid.Empty)
            {
                var branding = await _brandingGateway.GetTenantBrandingAsync(tenantId, ct);
                if (branding is not null)
                {
                    return new TenantBrandViewModel(branding.DisplayName, branding.LogoDataUrl, branding.FaviconDataUrl);
                }
            }
        }
        catch (Exception ex)
        {
            // Best-effort: a branding failure must never break the shell — fall back to the default brand.
            _logger.LogDebug(ex, "Tenant brand resolve failed; rendering default brand.");
        }

        return TenantBrandViewModel.Default;
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

public sealed record TenantBrandViewModel(string? DisplayName, string? LogoDataUrl, string? FaviconDataUrl)
{
    public static readonly TenantBrandViewModel Default = new(null, null, null);

    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoDataUrl);

    public bool HasFavicon => !string.IsNullOrWhiteSpace(FaviconDataUrl);
}

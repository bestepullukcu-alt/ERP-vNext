using Diten.HumanCapitalService.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace Diten.HumanCapitalService.Infrastructure.Tenancy;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public Guid? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var tenantValue = httpContext?.User.FindFirst("tenant_id")?.Value
                ?? httpContext?.User.FindFirst("tenantId")?.Value
                ?? httpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();

            return Guid.TryParse(tenantValue, out var tenantId) ? tenantId : null;
        }
    }
}

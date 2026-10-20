using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.CrmService.Infrastructure.Audit;

/// <summary>
/// WP-VW-W6 F8-a (AUD-001 K4, path b) — CRM's settings for the shared Building.Blocks forwarder: Platform DIRECTLY
/// (<c>PlatformService:BaseUrl</c> — the internal endpoint is not on the Gateway) with the shared internal key
/// (<c>AuthService:InternalApiKey</c>, the same pair the notification client uses). No fallback address: unset is the
/// forwarder's start-up warning and a warning per record (K8). The existing I*AuditPublisher seams are unchanged (F8-b).
/// </summary>
public static class CrmAuditForwarding
{
    public const string SourceService = "Diten.CrmService";

    public static IServiceCollection AddCrmAuditForwarding(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPlatformAuditForwarding();
        services.Configure<PlatformAuditForwarderOptions>(options =>
        {
            options.BaseUrl = configuration["PlatformService:BaseUrl"];
            options.InternalApiKey = configuration["AuthService:InternalApiKey"];
            options.SourceService = SourceService;
        });
        services.Replace(ServiceDescriptor.Scoped<IAuditCallContext, CrmAuditCallContext>());
        return services;
    }
}

/// <summary>
/// The record's tenant is CRM's server-resolved tenant (<see cref="ITenantContext"/>), not a token claim; actor and
/// correlation are the shared default's.
/// </summary>
public sealed class CrmAuditCallContext : IAuditCallContext
{
    private readonly HttpAuditCallContext _http;
    private readonly ITenantContext _tenant;

    public CrmAuditCallContext(HttpAuditCallContext http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public AuditCaller Resolve() => _http.Resolve() with { TenantId = _tenant.TenantId };
}

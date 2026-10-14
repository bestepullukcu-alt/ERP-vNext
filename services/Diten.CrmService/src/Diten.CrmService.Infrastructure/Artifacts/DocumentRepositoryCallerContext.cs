using System.Net.Http.Headers;
using System.Security.Claims;
using Diten.CrmService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.Artifacts;

/// <summary>
/// The caller context every MOD-0262-FU01 document repository call carries (SCMM-16B transport, shared by the content
/// artifact store and the WP-VW-W3 visit evidence store): the Gateway base address, the caller's <c>Authorization</c> /
/// <c>X-Tenant-Id</c> / <c>X-Correlation-Id</c> forwarded so the repository authorises and tenant-scopes as the same user,
/// and the server-derived CompanyId (the first company claim; never a payload value).
/// </summary>
internal static class DocumentRepositoryCallerContext
{
    public const string ObjectsPath = "api/v1/document-repository/objects";

    private static readonly string[] CompanyClaimTypes =
        ["companyId", "company_id", "legalEntityId", "legal_entity_id"];

    public static Uri GatewayBase(IConfiguration configuration)
    {
        var gatewayBaseUrl = configuration["Gateway:BaseUrl"]
            ?? configuration["GatewayUrl"]
            ?? "http://localhost:5000";
        return new Uri(gatewayBaseUrl.TrimEnd('/') + "/");
    }

    public static Guid ResolveCompanyId(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return Guid.Empty;
        }

        foreach (var type in CompanyClaimTypes)
        {
            var value = user.FindFirstValue(type);
            if (Guid.TryParse(value, out var companyId) && companyId != Guid.Empty)
            {
                return companyId;
            }
        }

        return Guid.Empty;
    }

    public static void Forward(HttpRequestMessage request, IHttpContextAccessor httpContextAccessor, ITenantContext tenantContext)
    {
        var context = httpContextAccessor.HttpContext;

        var authorization = context?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            request.Headers.Authorization = parsed;
        }

        var tenant = context?.Request.Headers["X-Tenant-Id"].ToString();
        if (string.IsNullOrWhiteSpace(tenant) && tenantContext.TenantId is { } tenantId)
        {
            tenant = tenantId.ToString();
        }

        if (!string.IsNullOrWhiteSpace(tenant))
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
        }

        var correlation = context?.Request.Headers["X-Correlation-Id"].ToString();
        if (string.IsNullOrWhiteSpace(correlation))
        {
            correlation = context?.TraceIdentifier;
        }

        if (!string.IsNullOrWhiteSpace(correlation))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation);
        }
    }
}

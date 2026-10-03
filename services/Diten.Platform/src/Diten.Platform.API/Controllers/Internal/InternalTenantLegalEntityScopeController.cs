using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/tenants")]
public sealed class InternalTenantLegalEntityScopeController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private readonly IDataScopeResolver _dataScopeResolver;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly IInternalScopeResolutionContext _internalScopeContext;

    public InternalTenantLegalEntityScopeController(
        IDataScopeResolver dataScopeResolver,
        IConfiguration configuration,
        ITenantContext tenantContext,
        IInternalScopeResolutionContext internalScopeContext)
    {
        _dataScopeResolver = dataScopeResolver;
        _configuration = configuration;
        _tenantContext = tenantContext;
        _internalScopeContext = internalScopeContext;
    }

    [HttpGet("{tenantId:guid}/users/{userId:guid}/legal-entity-scope")]
    public async Task<IActionResult> Resolve(Guid tenantId, Guid userId, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            return CreateActionResultInstance(Response<Resolution>.Fail("Unauthorized.", 401));
        }

        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return CreateActionResultInstance(Response<Resolution>.Fail("Tenant and actor identifiers are required.", 400));
        }

        // /api/internal is intentionally bypassed by the global tenant middleware. Bind the route tenant only
        // after the Auth-service credential has been verified and before tenant-filtered repositories are resolved.
        // The request never supplies or selects a Legal Entity.
        _tenantContext.SetTenant(tenantId);
        _internalScopeContext.Bind(tenantId, userId);

        var legalEntityIds = (await _dataScopeResolver.ResolveAsync(
                tenantId, userId, moduleCode: string.Empty, featureCode: null, ct))
            .Where(scope => scope.Kind == EntitlementDataScopeKind.LegalEntity && scope.ScopeId.HasValue)
            .Select(scope => scope.ScopeId!.Value)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var resolution = legalEntityIds.Length == 1
            ? new Resolution(legalEntityIds[0], "single")
            : new Resolution(null, legalEntityIds.Length == 0 ? "none" : "ambiguous");

        return CreateActionResultInstance(Response<Resolution>.Success(resolution));
    }

    private bool IsInternalRequestAuthorized()
    {
        var expected = _configuration["AuthService:InternalApiKey"];
        var provided = Request.Headers[InternalApiKeyHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    public sealed record Resolution(Guid? LegalEntityId, string Disposition);
}

using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleRegistration;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

/// <summary>
/// S2S module self-registration. Lives under <c>/api/internal/*</c> (bypassed by TenantResolutionMiddleware, which
/// would otherwise 403 a keyed call with no platform_admin JWT). Per-service credential gated. Sets platform context
/// (Guid.Empty) so the tenant-scoped page/action descriptor repositories operate in the same scope the catalog UI uses.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/module-catalog")]
public sealed class InternalModuleRegistrationController : CustomBaseController
{
    private const string CredentialIdHeader = "X-Module-Registration-Credential-Id";
    private const string CredentialSecretHeader = "X-Module-Registration-Credential";
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IMediator _mediator;
    private readonly IModuleRegistrationCredentialAuthenticator _credentialAuthenticator;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<InternalModuleRegistrationController> _logger;

    public InternalModuleRegistrationController(
        IMediator mediator,
        IModuleRegistrationCredentialAuthenticator credentialAuthenticator,
        IConfiguration configuration,
        ITenantContext tenantContext,
        ILogger<InternalModuleRegistrationController> logger)
    {
        _mediator = mediator;
        _credentialAuthenticator = credentialAuthenticator;
        _configuration = configuration;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpPost("register-manifest")]
    public async Task<IActionResult> RegisterManifest([FromBody] ModuleManifestDocument manifest, CancellationToken ct)
    {
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.ModuleCode))
        {
            return CreateActionResultInstance(Response<ModuleManifestReconcileResult>.Fail("ModuleCode is required.", 400));
        }

        var normalizedModuleCode = ModuleCatalogCodeNormalizer.Normalize(manifest.ModuleCode);
        var isMdmCredentialModule = string.Equals(normalizedModuleCode, "PRODUCT-ITEM-SKU-MASTER", StringComparison.Ordinal)
            || string.Equals(normalizedModuleCode, "LEGAL-ENTITY", StringComparison.Ordinal);
        var hasMdmCredentialHeaders = Request.Headers.ContainsKey(CredentialIdHeader)
            || Request.Headers.ContainsKey(CredentialSecretHeader);

        string? trustedProducerOwnerCode = null;
        if (isMdmCredentialModule)
        {
            var authentication = _credentialAuthenticator.Authenticate(
                Request.Headers[CredentialIdHeader].FirstOrDefault(),
                Request.Headers[CredentialSecretHeader].FirstOrDefault());
            if (!authentication.IsAuthenticated || string.IsNullOrWhiteSpace(authentication.ProducerOwnerCode))
            {
                return CreateActionResultInstance(Response<ModuleManifestReconcileResult>.Fail("Unauthorized.", 401));
            }

            trustedProducerOwnerCode = authentication.ProducerOwnerCode;
        }
        else if (hasMdmCredentialHeaders || !IsLegacyInternalRequestAuthorized())
        {
            return CreateActionResultInstance(Response<ModuleManifestReconcileResult>.Fail("Unauthorized.", 401));
        }

        // /api/internal bypasses tenant resolution; the catalog UI stores page/action descriptors under the platform
        // context (Guid.Empty). Match it so the reconcile reads/writes the same scope.
        _tenantContext.SetPlatformContext(Guid.Empty);

        var response = await _mediator.Send(
            new RegisterModuleManifestCommand(manifest, trustedProducerOwnerCode),
            ct);
        _logger.LogInformation(
            "Module manifest registered. ModuleCode={ModuleCode} Success={Success}",
            manifest.ModuleCode,
            response.IsSuccessful);
        return CreateActionResultInstance(response);
    }

    private bool IsLegacyInternalRequestAuthorized()
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

}

using System.Net.Http.Headers;
using Diten.MdmService.Api.Security;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.LegalEntity.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/tenants")]
public sealed class InternalLegalEntityReferenceController : CustomBaseController
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string ActorHeader = "X-Actor-Id";

    private readonly IPlatformServiceTokenValidator _serviceTokenValidator;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;

    public InternalLegalEntityReferenceController(
        IPlatformServiceTokenValidator serviceTokenValidator,
        ITenantContext tenantContext,
        IMediator mediator)
    {
        _serviceTokenValidator = serviceTokenValidator;
        _tenantContext = tenantContext;
        _mediator = mediator;
    }

    [HttpGet("{tenantId:guid}/legal-entities/{legalEntityId:guid}/reference-validation")]
    public async Task<IActionResult> Validate(
        Guid tenantId,
        Guid legalEntityId,
        CancellationToken cancellationToken)
    {
        var tenantHeader = Request.Headers[TenantHeader].FirstOrDefault();
        var actorHeader = Request.Headers[ActorHeader].FirstOrDefault();
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        if (tenantId == Guid.Empty
            || legalEntityId == Guid.Empty
            || !Guid.TryParse(tenantHeader, out var headerTenantId)
            || headerTenantId != tenantId
            || !Guid.TryParse(actorHeader, out var actorId)
            || actorId == Guid.Empty
            || !AuthenticationHeaderValue.TryParse(authorization, out var parsed)
            || !string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(parsed.Parameter)
            || !_serviceTokenValidator.IsAuthorized(parsed.Parameter, tenantId, actorId, legalEntityId))
        {
            return Unauthorized();
        }

        // TenantResolutionMiddleware may parse X-Tenant-Id, but it is not authority. Repository access occurs
        // only after the signed Platform identity has bound the same tenant, actor and Legal Entity values.
        if (!_tenantContext.IsResolved || _tenantContext.TenantId != tenantId)
        {
            return Unauthorized();
        }

        var response = await _mediator.Send(
            new ValidateLegalEntityReferenceQuery(legalEntityId),
            cancellationToken);
        return CreateActionResultInstance(response);
    }
}

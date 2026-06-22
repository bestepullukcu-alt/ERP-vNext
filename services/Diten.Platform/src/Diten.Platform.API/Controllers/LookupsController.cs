using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Lookups.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Route("api/lookups")]
[Authorize(Policy = "PlatformActor")]
public sealed class LookupsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public LookupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("module-catalog/domains")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetModuleCatalogDomains(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("module-catalog/domains"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("module-catalog/services")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetModuleCatalogServices(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("module-catalog/services"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("module-catalog/permission-modules")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetModuleCatalogPermissionModules(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("module-catalog/permission-modules"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("countries")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetCountries(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("countries"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("currencies")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetCurrencies(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetCurrencyLookupQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("locales")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetLocales(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLocaleLookupQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("languages")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetLanguages(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLocaleLookupQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("timezones")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetTimezones(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTimezoneLookupQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("tenant-tiers")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetTenantTiers(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantTierLookupQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("feature-categories")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetFeatureCategories(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("feature-categories"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("subscription-cycles")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetSubscriptionCycles(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("subscription-cycles"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("audit/categories")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetAuditCategories(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("audit/categories"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("audit/operations")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetAuditOperations(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("audit/operations"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("audit/outcomes")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetAuditOutcomes(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("audit/outcomes"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{lookupKey}")]
    [HasPermission("platform.lookups.read")]
    public async Task<IActionResult> GetLookupByKey(string lookupKey, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery(lookupKey), ct);
        return CreateActionResultInstance(response);
    }
}

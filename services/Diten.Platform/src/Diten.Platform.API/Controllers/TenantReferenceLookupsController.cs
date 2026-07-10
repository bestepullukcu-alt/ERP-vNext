using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Features.Lookups.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

// MOD-0220 fix — universal ISO reference lookups (countries, currencies) that TENANT actors must read for the
// Legal Entity wizard. The main LookupsController is [Authorize(Policy = "PlatformActor")] and 403s tenant_user
// actors (İş3 tenant-vs-platform boundary); an action-level [Authorize] cannot relax a class-level policy, so
// these two universal, non-sensitive keys live here under [Authorize] (ANY authenticated actor — platform_admin
// OR tenant_user). Deliberately scoped to countries/currencies ONLY — this does NOT open the rest of the
// platform lookup surface to tenants. Sits under the existing "/api/lookups/{everything}" gateway route
// (sub-path "reference/…"), so no gateway change is required.
[ApiController]
[Route("api/lookups/reference")]
[Authorize]
public sealed class TenantReferenceLookupsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TenantReferenceLookupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLookupOptionsQuery("countries"), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetCurrencyLookupQuery(), ct);
        return CreateActionResultInstance(response);
    }
}

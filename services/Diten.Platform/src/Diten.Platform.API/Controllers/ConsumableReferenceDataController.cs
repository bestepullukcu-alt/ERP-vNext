using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Infrastructure.Persistence.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Controllers;

// WP-BRD-TENANT-CRM-SETS — the reference sets the consuming services (CRM first; mobile and Web through CRM) validate
// against, readable by EVERY signed-in tenant user. The Platform consumer path (api/v1/reference-data/sets/...) needs
// Platform.BusinessReferenceData.Consumer.Read, which only an administrator holds; that permission is NOT handed to
// tenant roles here — this route answers only the allow-listed sets (BusinessReferenceData:ConsumableSets).
//
// Unlike TenantReferenceDataController (sets/{setCode}, three Global sets, always read in the reference tenant — kept
// as it is), a set is read by its OWN BRD scope: a tenant-scoped set in the caller's tenant, a global one from the
// global source. The caller's tenant comes from the validated token (/api/lookups is a bypass path of the tenant
// middleware, see TenantUserCaller); a client scope_key is never read.
// Sits under the existing "/api/lookups/{everything}" gateway route — no gateway change.
[ApiController]
[Route("api/lookups/reference-data")]
[Authorize]
public sealed class ConsumableReferenceDataController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly BusinessReferenceDataCatalogLoadOptions _catalogOptions;

    public ConsumableReferenceDataController(
        IMediator mediator,
        IOptions<BusinessReferenceDataCatalogLoadOptions> catalogOptions)
    {
        _mediator = mediator;
        _catalogOptions = catalogOptions.Value;
    }

    [HttpGet("consumable-sets/{setCode}/published-values")]
    [LoginOnly("Published values of the allow-listed reference sets the consuming services validate against (BusinessReferenceData:ConsumableSets); any signed-in tenant user, read in the caller's own tenant by the set's scope (see class note).")]
    public async Task<IActionResult> GetPublishedValues(string setCode, CancellationToken ct)
    {
        var caller = TenantUserCaller.Resolve(User, Request.Headers);
        if (caller.Refusal is { } refusal)
        {
            return CreateActionResultInstance(
                Response<BusinessReferenceDataPublishedValuesModel>.Fail(refusal, 400, refusal));
        }

        // The global source is the SAME reference tenant the catalog seed and the sets/{setCode} stopgap use.
        Guid? referenceTenantId = Guid.TryParse(_catalogOptions.TenantId, out var parsed) && parsed != Guid.Empty
            ? parsed
            : null;

        var response = await _mediator.Send(
            new GetConsumableBusinessReferenceDataPublishedValuesQuery(setCode, caller.TenantId, referenceTenantId), ct);
        return CreateActionResultInstance(response);
    }
}

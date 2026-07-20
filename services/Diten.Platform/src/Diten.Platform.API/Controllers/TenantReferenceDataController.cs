using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Persistence.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Controllers;

// MOD-0220 — the Legal Entity wizard (a tenant actor) reads its Legal Form / Country / Base Currency options from
// governed Business Reference Data. Two things make this non-trivial:
//  1) The main BRD consumer endpoint requires Platform.BusinessReferenceData.Consumer.Read (tenant roles lack it
//     → 403). This controller exposes ONLY the published values of the specific Global reference sets the LE
//     wizard needs, to ANY authenticated actor ([Authorize], no permission gate), allow-listed so it can't read
//     other BRD sets.
//  2) BRD sets are stored PER-TENANT and the catalog seed loads them under the REFERENCE tenant
//     (BusinessReferenceData:CatalogLoad:TenantId). The repository scopes reads to the AMBIENT (caller's) tenant,
//     so a tenant user would find nothing. STOPGAP: for these allow-listed Global sets only, resolve the read in
//     the REFERENCE tenant's context — sourced from the SAME config key the seed uses, so seed + read stay in
//     lock-step. (Making BRD "Global" natively cross-tenant is backlogged.)
// Sits under the existing "/api/lookups/{everything}" gateway route — no gateway change.
[ApiController]
[Route("api/lookups/reference-data")]
[Authorize]
public sealed class TenantReferenceDataController : CustomBaseController
{
    // Exactly the universal reference sets the LE wizard consumes. Do NOT broaden without review — see class note.
    private static readonly HashSet<string> TenantReadableSets =
        new(StringComparer.OrdinalIgnoreCase) { "legal-form", "country", "base-currency" };

    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly BusinessReferenceDataCatalogLoadOptions _catalogOptions;

    public TenantReferenceDataController(
        IMediator mediator,
        ITenantContext tenantContext,
        IOptions<BusinessReferenceDataCatalogLoadOptions> catalogOptions)
    {
        _mediator = mediator;
        _tenantContext = tenantContext;
        _catalogOptions = catalogOptions.Value;
    }

    [HttpGet("sets/{setCode}/published-values")]
    public async Task<IActionResult> GetPublishedValues(string setCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(setCode) || !TenantReadableSets.Contains(setCode))
        {
            return CreateActionResultInstance(
                Response<BusinessReferenceDataPublishedValuesModel>.Fail("reference_set_not_tenant_accessible", 404));
        }

        // The seeded Global sets live under the reference tenant — read them there, not under the caller's tenant.
        if (!Guid.TryParse(_catalogOptions.TenantId, out var referenceTenantId) || referenceTenantId == Guid.Empty)
        {
            return CreateActionResultInstance(
                Response<BusinessReferenceDataPublishedValuesModel>.Fail("reference_tenant_misconfigured", 500));
        }

        using (TenantScope.Begin(_tenantContext, referenceTenantId))
        {
            var response = await _mediator.Send(new GetBusinessReferenceDataPublishedValuesQuery(setCode, null), ct);
            return CreateActionResultInstance(response);
        }
    }
}

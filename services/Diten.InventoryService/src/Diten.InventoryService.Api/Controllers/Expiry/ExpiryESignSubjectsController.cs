using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Expiry;
using Diten.InventoryService.Application.Features.Expiry.Handlers;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.InventoryService.Api.Controllers.Expiry;

/// <summary>
/// FEFO-BUNDLE <c>getSignableSubject</c> (F3) — the MOD-0022 subject provider (ESIGN-BUNDLE §6), called by Platform with the SIGNER's token
/// and <c>X-Tenant-Id</c> (config <c>ESign:SubjectProviders:expiry:BaseUrl</c>). The MOD-0175 <c>QuarantineESignSubjectsController</c>
/// pattern; every refusal is one 404:
/// <list type="bullet">
/// <item>the key of the type is missing — <c>expiry.exceptions.read</c> for <c>expiry.exception</c>, <c>expiry.date-overrides.create</c>
/// for <c>expiry.date-override</c>;</item>
/// <item>an unknown type, a subject not of the caller's tenant, a company named by the caller that is not the record's;</item>
/// <item>an override prepared by another user.</item>
/// </list>
/// With no company named (MOD-0022 sends none), the scope middleware lets this read-only path through and the record's own company is
/// taken (ASSUMPTION-0175-Q3-02 pattern).
/// </summary>
[Route("api/expiry/esign-subjects")]
[Authorize]
public sealed class ExpiryESignSubjectsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ExpiryESignSubjectsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{subjectType}/{subjectId}")]
    public async Task<IActionResult> GetSignableSubject(string subjectType, string subjectId, CancellationToken ct)
    {
        var key = subjectType switch
        {
            ExpirySignableActions.ExceptionSubjectType => ExpiryPermissions.ExceptionsRead,
            ExpirySignableActions.DateOverrideSubjectType => ExpiryPermissions.DateOverridesCreate,
            _ => null
        };
        if (key is null || !User.Claims.Any(c => c.Type == "permission" && c.Value == key)
            || !Guid.TryParseExact(subjectId, "D", out var id))
        {
            return SubjectNotFound();
        }

        var tenant = HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        var tenantId = tenant.IsResolved ? tenant.TenantId : Claim("tenant_id");
        var userId = tenant.IsResolved ? tenant.UserId : Claim("sub", System.Security.Claims.ClaimTypes.NameIdentifier);
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return SubjectNotFound();
        }

        var subject = await HttpContext.RequestServices.GetRequiredService<IExpirySubjectDirectory>().SubjectAsync(tenantId, subjectType, id, ct);
        if (subject is not { } found || (tenant.IsResolved && tenant.LegalEntityId != found.LegalEntityId) || (found.Owner is { } owner && owner != userId))
        {
            return SubjectNotFound();
        }

        if (!tenant.IsResolved)
        {
            // set before the handler (and the company-scoped repository it needs) is resolved
            tenant.SetScope(tenantId, found.LegalEntityId, userId, LegalEntitySources.SubjectRecord);
        }

        return CreateActionResultInstance(await _mediator.Send(new GetExpirySignableSubjectQuery(subjectType, id), ct));
    }

    private Guid Claim(params string[] types)
    {
        var values = User.Claims.Where(c => types.Contains(c.Type, StringComparer.Ordinal)).Select(c => c.Value).ToList();
        return values.Count == 1 && Guid.TryParse(values[0], out var value) ? value : Guid.Empty;
    }

    private IActionResult SubjectNotFound() => ContractFailure(StatusCodes.Status404NotFound, ExpiryErrorCodes.ExceptionNotFound, "Signable subject not found.");
}

using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.BusinessReferenceData;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

/// <summary>
/// WP-PLT-ITEM-LISTS-01 (MOD-0048-FU01 named step "Universal Item Reference Lists for MOD-0290-FU04"): exact resolution
/// of the item universal catalog (ITEM-UNIVERSAL-Vn) for MDM. No new trust path: the verified-reference executor with the existing MDM resolver
/// credential and header audience (as the verified-market controller), plus a tenant JWT or MDM's trusted service
/// token. Anonymous at the MVC layer by design — the executor refuses every unauthenticated request by name.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/v1/reference-data/item-universal")]
public sealed class InternalItemUniversalReferenceDataController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly VerifiedReferenceDataRequestExecutor _requestExecutor;

    public InternalItemUniversalReferenceDataController(IMediator mediator,
        IVerifiedGskuResolverCredentialAuthenticator credentialAuthenticator,
        IVerifiedGskuResolverJwtTenantContext jwtTenantContext,
        ITenantContext tenantContext,
        IVerifiedReferenceDataServiceTenantContext? serviceTenantContext = null)
    {
        _mediator = mediator;
        _requestExecutor = new VerifiedReferenceDataRequestExecutor(
            credentialAuthenticator, jwtTenantContext, serviceTenantContext, tenantContext);
    }

    // FIX1 M1: 200 short codes fit well inside 64 KB; a larger body is refused by the server before the action.
    public const long MaximumRequestBytes = 65536;

    [HttpPost("resolve")]
    [RequestSizeLimit(MaximumRequestBytes)]
    public Task<IActionResult> Resolve([FromBody] ItemUniversalResolveRequest? request, CancellationToken cancellationToken) =>
        _requestExecutor.ExecuteAsync(HttpContext, cancellationToken, async (_, token) =>
        {
            if (Request.Query.Count > 0 || request?.Selections is null || request.AdditionalFields is { Count: > 0 }
                || request.Selections.Any(selection => selection?.AdditionalFields is { Count: > 0 }))
            {
                return Failure(400, ResolveItemUniversalReferenceDataHandler.ContractInvalid);
            }

            var query = new ResolveItemUniversalReferenceDataQuery(request.Selections
                .Select(selection => selection is null
                    ? null
                    : new ItemUniversalResolveSelectionInput(selection.SetCode ?? string.Empty,
                        selection.ValueCode ?? string.Empty, selection.ResolutionMode ?? string.Empty))
                .ToList());
            return CreateActionResultInstance(await _mediator.Send(query, token));
        }, Failure);

    // MOD-0290-FU04 S5b (BL-600): the values of the asked sets (only the three item sets) for MDM's item / stock forms —
    // the SAME executor, credential and audience as the resolve (no new trust path); a list to choose from, never the
    // proof of a choice (MDM resolves each chosen code exactly).
    [HttpPost("enumerate")]
    [RequestSizeLimit(MaximumRequestBytes)]
    public Task<IActionResult> Enumerate([FromBody] ItemUniversalEnumerateRequest? request, CancellationToken cancellationToken) =>
        _requestExecutor.ExecuteAsync(HttpContext, cancellationToken, async (_, token) =>
        {
            if (Request.Query.Count > 0 || request?.SetCodes is null || request.AdditionalFields is { Count: > 0 })
            {
                return EnumerationFailure(400, ResolveItemUniversalReferenceDataHandler.ContractInvalid);
            }

            return CreateActionResultInstance(await _mediator.Send(
                new EnumerateItemUniversalReferenceDataQuery(request.SetCodes), token));
        }, EnumerationFailure);

    private IActionResult EnumerationFailure(int statusCode, string code) => CreateActionResultInstance(
        Response<ItemUniversalEnumerationResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));

    private IActionResult Failure(int statusCode, string code) => CreateActionResultInstance(
        Response<ItemUniversalResolveResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));
}

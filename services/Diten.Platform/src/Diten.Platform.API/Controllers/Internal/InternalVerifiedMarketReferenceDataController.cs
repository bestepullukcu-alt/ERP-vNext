using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.BusinessReferenceData;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/v1/reference-data/verified-market")]
public sealed class InternalVerifiedMarketReferenceDataController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly IVerifiedReferenceDataRequestExecutor _requestExecutor;

    public InternalVerifiedMarketReferenceDataController(IMediator mediator,
        IVerifiedGskuResolverCredentialAuthenticator credentialAuthenticator,
        IVerifiedGskuResolverJwtTenantContext jwtTenantContext,
        ITenantContext tenantContext)
    {
        _mediator = mediator;
        _requestExecutor = new VerifiedReferenceDataRequestExecutor(credentialAuthenticator, jwtTenantContext, tenantContext);
    }

    [HttpPost("resolve")]
    public Task<IActionResult> Resolve([FromBody] BusinessReferenceDataVerifiedMarketResolveRequest? request,
        CancellationToken cancellationToken) =>
        _requestExecutor.ExecuteAsync(HttpContext, cancellationToken, async (_, token) =>
        {
            if (Request.Query.Count > 0 || request?.MarketCode is null || request.AdditionalFields is { Count: > 0 })
            {
                return ResolveFailure(409, "REFERENCE_CONTRACT_MISMATCH");
            }
            return CreateActionResultInstance(await _mediator.Send(new ResolveVerifiedMarketReferenceDataQuery(request.MarketCode), token));
        }, ResolveFailure);

    [HttpPost("enumerate-active")]
    public Task<IActionResult> EnumerateActive(CancellationToken cancellationToken) =>
        _requestExecutor.ExecuteAsync(HttpContext, cancellationToken, async (_, token) =>
        {
            if (Request.Query.Count > 0 || Request.ContentLength is > 0 || Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                return EnumerationFailure(409, "REFERENCE_CONTRACT_MISMATCH");
            }
            return CreateActionResultInstance(await _mediator.Send(new EnumerateVerifiedMarketsQuery(), token));
        }, EnumerationFailure);

    private IActionResult ResolveFailure(int statusCode, string code) => CreateActionResultInstance(
        Response<BusinessReferenceDataVerifiedMarketResolveResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));
    private IActionResult EnumerationFailure(int statusCode, string code) => CreateActionResultInstance(
        Response<BusinessReferenceDataVerifiedMarketsResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));
}

using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.BusinessReferenceData;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/v1/reference-data/verified-gsku")]
public sealed class InternalBusinessReferenceDataController : CustomBaseController
{
    public const string CredentialIdHeader = "X-Verified-Gsku-Credential-Id";
    public const string CredentialSecretHeader = "X-Verified-Gsku-Credential";
    public const string AudienceHeader = "X-Verified-Gsku-Audience";

    private readonly IMediator _mediator;
    private readonly IVerifiedReferenceDataRequestExecutor _requestExecutor;

    public InternalBusinessReferenceDataController(
        IMediator mediator,
        IVerifiedGskuResolverCredentialAuthenticator credentialAuthenticator,
        IVerifiedGskuResolverJwtTenantContext jwtTenantContext,
        ITenantContext tenantContext)
    {
        _mediator = mediator;
        _requestExecutor = new VerifiedReferenceDataRequestExecutor(credentialAuthenticator, jwtTenantContext, tenantContext);
    }

    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve(
        [FromBody] BusinessReferenceDataVerifiedResolveRequest? request,
        CancellationToken cancellationToken)
    {
        return await _requestExecutor.ExecuteAsync(HttpContext, cancellationToken, async (_, token) =>
        {
            if (Request.Query.Count > 0 || request?.Selections is null
                || request.AdditionalFields is { Count: > 0 }
                || request.Selections.Any(x => x.AdditionalFields is { Count: > 0 }))
            {
                return Failure(409, "REFERENCE_RESOLUTION_CONTRACT_INVALID");
            }

            var query = new ResolveVerifiedGskuReferenceDataQuery(request.Selections.Select(x =>
                new BusinessReferenceDataVerifiedResolveSelectionInput(x.SetCode ?? string.Empty, x.ValueCode ?? string.Empty,
                    x.ResolutionMode ?? string.Empty)).ToList());
            return CreateActionResultInstance(await _mediator.Send(query, token));
        }, Failure);
    }

    [HttpPost("enumerate-uom")]
    public async Task<IActionResult> EnumerateUom(CancellationToken cancellationToken)
    {
        return await _requestExecutor.ExecuteAsync(HttpContext, cancellationToken, async (_, token) =>
        {
            if (Request.Query.Count > 0 || Request.ContentLength is > 0 || Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                return EnumerationFailure(409, "REFERENCE_CONTRACT_MISMATCH");
            }
            return CreateActionResultInstance(await _mediator.Send(new EnumerateVerifiedGskuUomsQuery(), token));
        }, EnumerationFailure);
    }

    private IActionResult Failure(int statusCode, string code) =>
        CreateActionResultInstance(
            Response<BusinessReferenceDataVerifiedResolveResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));

    private IActionResult EnumerationFailure(int statusCode, string code) =>
        CreateActionResultInstance(
            Response<BusinessReferenceDataVerifiedUomResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));
}

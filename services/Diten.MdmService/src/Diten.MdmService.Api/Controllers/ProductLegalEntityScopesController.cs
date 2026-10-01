using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/global-products/{globalProductId:guid}/legal-entity-scope-policy")]
public sealed class ProductLegalEntityScopesController : CustomBaseController
{
    private const string ReadPermission = "mdm.product-legal-entity-scopes.read";
    private const string ConfigurePermission = "mdm.product-legal-entity-scopes.configure";
    private const string ReplacePermission = "mdm.product-legal-entity-scopes.replace";
    private const string EndPermission = "mdm.product-legal-entity-scopes.end";
    private const string GlobalProductsReadPermission = "mdm.global-products.read";
    private const string LegalEntitiesReadPermission = "mdm.legal-entities.read";

    private readonly IMediator _mediator;

    public ProductLegalEntityScopesController(IMediator mediator) =>
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    [HttpGet]
    [HasPermission(ReadPermission)]
    public async Task<IActionResult> GetPolicy(
        Guid globalProductId,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetProductLegalEntityScopePolicyQuery(globalProductId),
            cancellationToken));

    [HttpGet("history")]
    [HasPermission(ReadPermission)]
    public async Task<IActionResult> GetHistory(
        Guid globalProductId,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetProductLegalEntityScopeHistoryQuery(globalProductId),
            cancellationToken));

    [HttpGet("effective")]
    [HasPermission(ReadPermission)]
    public async Task<IActionResult> GetEffectiveFacts(
        Guid globalProductId,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetEffectiveProductLegalEntityScopeQuery(globalProductId),
            cancellationToken));

    [HttpGet("create-options")]
    [HasPermission(ConfigurePermission)]
    [Authorize(Policy = $"Permission:{GlobalProductsReadPermission}")]
    [Authorize(Policy = $"Permission:{LegalEntitiesReadPermission}")]
    public async Task<IActionResult> GetCreateOptions(
        Guid globalProductId,
        CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetProductLegalEntityScopeCreateOptionsQuery(globalProductId),
            cancellationToken));

    [HttpPost]
    [HasPermission(ConfigurePermission)]
    public async Task<IActionResult> CreatePolicy(
        Guid globalProductId,
        [FromBody] ProductLegalEntityScopeModels.CreatePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || HasUnknownFields(request.UnmappedFields))
        {
            return InvalidRequest();
        }
        if (!TryGetCommandId(out var commandId))
        {
            return InvalidIdempotencyKey();
        }

        return CreateActionResultInstance(await _mediator.Send(
            new CreateProductLegalEntityScopePolicyCommand(globalProductId, commandId, request),
            cancellationToken));
    }

    [HttpPost("replace")]
    [HasPermission(ReplacePermission)]
    public async Task<IActionResult> ReplacePolicy(
        Guid globalProductId,
        [FromBody] ProductLegalEntityScopeModels.ReplacePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || HasUnknownFields(request.UnmappedFields))
        {
            return InvalidRequest();
        }
        if (!TryGetCommandId(out var commandId))
        {
            return InvalidIdempotencyKey();
        }

        return CreateActionResultInstance(await _mediator.Send(
            new ReplaceProductLegalEntityScopePolicyCommand(globalProductId, commandId, request),
            cancellationToken));
    }

    [HttpPost("end")]
    [HasPermission(EndPermission)]
    public async Task<IActionResult> EndPolicy(
        Guid globalProductId,
        [FromBody] ProductLegalEntityScopeModels.EndPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || HasUnknownFields(request.UnmappedFields))
        {
            return InvalidRequest();
        }
        if (!TryGetCommandId(out var commandId))
        {
            return InvalidIdempotencyKey();
        }

        return CreateActionResultInstance(await _mediator.Send(
            new EndProductLegalEntityScopePolicyCommand(globalProductId, commandId, request),
            cancellationToken));
    }

    private bool TryGetCommandId(out Guid commandId)
    {
        commandId = Guid.Empty;
        var values = Request.Headers["Idempotency-Key"];
        return values.Count == 1
            && Guid.TryParseExact(values[0], "D", out commandId)
            && commandId != Guid.Empty;
    }

    private static bool HasUnknownFields(IDictionary<string, System.Text.Json.JsonElement>? fields) =>
        fields is { Count: > 0 };

    private IActionResult InvalidRequest() =>
        CreateActionResultInstance(Response<NoContent>.Fail("PRODUCT_SCOPE_REQUEST_INVALID", 400));

    private IActionResult InvalidIdempotencyKey() =>
        CreateActionResultInstance(Response<NoContent>.Fail("IDEMPOTENCY_KEY_INVALID", 400));
}

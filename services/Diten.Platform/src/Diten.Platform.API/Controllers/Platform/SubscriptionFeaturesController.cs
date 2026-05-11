using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.SubscriptionFeatures;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Application.Features.SubscriptionFeatures.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/subscription-features")]
[Authorize(Policy = "PlatformActor")]
public sealed class SubscriptionFeaturesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SubscriptionFeaturesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("Platform.SubscriptionFeatures.Read")]
    public async Task<IActionResult> GetCatalog([FromQuery] FeatureCatalogFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetFeatureCatalogQuery(filter), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("Platform.SubscriptionFeatures.Read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetFeatureDefinitionByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("Platform.SubscriptionFeatures.Create")]
    public async Task<IActionResult> Create([FromBody] CreateFeatureDefinitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateFeatureDefinitionCommand(request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("Platform.SubscriptionFeatures.Update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFeatureDefinitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateFeatureDefinitionCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}/plan-mappings")]
    [HasPermission("Platform.SubscriptionFeatures.Read")]
    public async Task<IActionResult> GetPlanMappings(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetFeaturePlanMappingsQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/archive")]
    [HasPermission("Platform.SubscriptionFeatures.Archive")]
    public async Task<IActionResult> Archive(Guid id, [FromBody] ArchiveFeatureDefinitionRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ArchiveFeatureDefinitionCommand(id, request?.RowVersion), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission("Platform.SubscriptionFeatures.Update")]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] ArchiveFeatureDefinitionRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeactivateFeatureDefinitionCommand(id, request?.RowVersion), ct);
        return CreateActionResultInstance(response);
    }
}

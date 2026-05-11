using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Application.Features.SubscriptionPlans.Queries;
using Diten.Platform.Application.Features.SubscriptionFeatures;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Application.Features.SubscriptionFeatures.Queries;
using Diten.Platform.Application.Features.Tenants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/subscription-plans")]
[Authorize(Policy = "PlatformActor")]
public sealed class SubscriptionPlansController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SubscriptionPlansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("Platform.SubscriptionPlans.Read")]
    public async Task<IActionResult> GetPlans([FromQuery] SubscriptionPlanFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetSubscriptionPlansQuery(filter), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("active")]
    [HasPermission("Platform.SubscriptionPlans.Read")]
    public async Task<IActionResult> GetActivePlans(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetActiveSubscriptionPlansQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("by-module/{moduleKey}")]
    [HasPermission("Platform.SubscriptionPlans.Read")]
    public async Task<IActionResult> GetByIncludedModuleKey(string moduleKey, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetSubscriptionPlansByModuleKeyQuery(moduleKey), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("summary")]
    [HasPermission("Platform.SubscriptionPlans.Read")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetSubscriptionPlanSummaryQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("Platform.SubscriptionPlans.Read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetSubscriptionPlanByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("Platform.SubscriptionPlans.Create")]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionPlanRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateSubscriptionPlanCommand(request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("Platform.SubscriptionPlans.Update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionPlanRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateSubscriptionPlanCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/activate")]
    [HasPermission("Platform.SubscriptionPlans.Activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new ActivateSubscriptionPlanCommand(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission("Platform.SubscriptionPlans.Deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeactivateSubscriptionPlanCommand(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}/features")]
    [HasPermission("Platform.SubscriptionFeatures.Read")]
    public async Task<IActionResult> GetFeatures(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPlanFeatureMappingsQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}/features")]
    [HasPermission("Platform.SubscriptionFeatures.ManageMappings")]
    public async Task<IActionResult> UpdateFeatures(Guid id, [FromBody] UpdatePlanFeatureMappingsRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdatePlanFeatureMappingsCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }
}

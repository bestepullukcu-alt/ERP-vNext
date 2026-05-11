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
[Route("api/platform/feature-categories")]
[Authorize(Policy = "PlatformActor")]
public sealed class FeatureCategoriesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public FeatureCategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("Platform.SubscriptionFeatures.Read")]
    public async Task<IActionResult> GetCategories([FromQuery] string? status, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetFeatureCategoriesQuery(status), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("Platform.SubscriptionFeatures.Create")]
    public async Task<IActionResult> Create([FromBody] CreateFeatureCategoryRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateFeatureCategoryCommand(request), ct);
        return CreateActionResultInstance(response);
    }
}

using Diten.MdmService.Application.Features.Sample.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// POST /api/samples — JWT [Authorize] zorunlu.
/// Controller'da iş kuralı YOKTUR; yalnızca MediatR'a delege edilir.
/// </summary>
[ApiController]
[Route("api/samples")]
[Authorize]
public sealed class SampleController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public SampleController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    /// <summary>
    /// Yeni bir SampleEntity oluşturur.
    /// TenantId request body'de YOK — X-Tenant-Id header'dan otomatik alınır.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateSampleResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSampleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSampleCommand(request.Name, request.Description);
        var result = await _mediator.Send(command, cancellationToken);

        // Gateway arkasında doğru Location üretmek için
        var publicBaseUrl = _configuration["PublicBaseUrl"];

        // Eğer config yoksa (fail-safe): relative location dön
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return Created($"/api/samples?id={result.Id}", result);
        }

        // http://localhost:5000/services/mdm + /api/samples?id=...
        var location = $"{publicBaseUrl.TrimEnd('/')}/api/samples?id={result.Id}";
        return Created(location, result);
    }
}

/// <summary>
/// POST /api/samples request body.
/// TenantId ASLA bu record'da olmamalı.
/// </summary>
public sealed record CreateSampleRequest(
    string Name,
    string? Description
);
using Diten.Persistence.Context;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

/// <summary>Readiness-style check: verifies MongoDB is reachable.</summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly MongoDbContext _mongo;
    private readonly IConfiguration _configuration;

    public HealthController(MongoDbContext mongo, IConfiguration configuration)
    {
        _mongo = mongo;
        _configuration = configuration;
    }

    /// <summary>GET /health — returns 200 if MongoDB responds, 503 otherwise.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthOkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            await _mongo.PingAsync(cancellationToken);
            return Ok(new HealthOkResponse("Healthy", _configuration["DatabaseName"] ?? ""));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthErrorResponse(
                "Unhealthy",
                ex.Message));
        }
    }
}

public sealed record HealthOkResponse(string Status, string DatabaseName);

public sealed record HealthErrorResponse(string Status, string Error);

using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/platform-administrators")]
public sealed class InternalPlatformAdministratorsController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly IPlatformAdministratorRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalPlatformAdministratorsController> _logger;

    public InternalPlatformAdministratorsController(
        IPlatformAdministratorRepository repository,
        IConfiguration configuration,
        ILogger<InternalPlatformAdministratorsController> logger)
    {
        _repository = repository;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] string email, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning(
                "Internal platform administrator status request rejected. Email={Email} CorrelationId={CorrelationId}",
                email,
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return CreateActionResultInstance(Response<PlatformAdministratorStatusDto>.Fail("Unauthorized.", 401));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return CreateActionResultInstance(Response<PlatformAdministratorStatusDto>.Fail("Email is required.", 400));
        }

        var administrator = await _repository.GetByNormalizedEmailAsync(email, ct);
        if (administrator is null)
        {
            return CreateActionResultInstance(Response<PlatformAdministratorStatusDto>.Fail("Platform administrator not found.", 404));
        }

        var dto = new PlatformAdministratorStatusDto(
            administrator.Id,
            administrator.Email,
            administrator.Status.ToString(),
            administrator.IsDeleted,
            administrator.Status == AdministratorStatus.Active && !administrator.IsDeleted);

        return CreateActionResultInstance(Response<PlatformAdministratorStatusDto>.Success(dto));
    }

    [HttpPost("accept-login")]
    public async Task<IActionResult> AcceptLogin([FromBody] PlatformAdministratorLoginAcceptedRequest request, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning(
                "Internal platform administrator login-accept request rejected. Email={Email} CorrelationId={CorrelationId}",
                request.Email,
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return CreateActionResultInstance(Response<NoContent>.Fail("Unauthorized.", 401));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Email is required.", 400));
        }

        var administrator = await _repository.GetByNormalizedEmailAsync(request.Email, ct);
        if (administrator is null || administrator.IsDeleted || administrator.Status != AdministratorStatus.Active)
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Platform administrator not found or inactive.", 404));
        }

        administrator.InvitationStatus = AdministratorInvitationStatus.Accepted;
        administrator.LastLoginAtUtc = DateTimeOffset.UtcNow;
        administrator.UpdatedAt = DateTimeOffset.UtcNow;
        administrator.UpdatedBy = "auth-service";

        var updated = await _repository.UpdateAsync(administrator, administrator.Version, ct);
        return updated
            ? CreateActionResultInstance(Response<NoContent>.Success(204))
            : CreateActionResultInstance(Response<NoContent>.Fail("Platform administrator login state could not be updated.", 409));
    }

    private bool IsInternalRequestAuthorized()
    {
        var expected = _configuration["AuthService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue(InternalApiKeyHeader, out var providedValues))
        {
            return false;
        }

        var provided = providedValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private sealed record PlatformAdministratorStatusDto(
        Guid Id,
        string Email,
        string Status,
        bool IsDeleted,
        bool IsActive);

    public sealed record PlatformAdministratorLoginAcceptedRequest(string Email);
}

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
            administrator.Status == AdministratorStatus.Active && !administrator.IsDeleted,
            // BL-521 FIX1 F1(b) — the record's actor type, so Auth re-issues a refreshed token as what the record says
            // (a demoted platform administrator becomes partner_admin at its next refresh), not as the old token said.
            administrator.ActorType.ToString(),
            // FIX4 item 2 — the record's roles, so every platform sign-in and refresh converges Auth's copy of them to the
            // record before minting (a sync that was lost no longer outlives the next token).
            administrator.Roles.Select(role => role.ToString()).ToList());

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

        if (string.Equals(administrator.NormalizedEmail, "admin@diten.com", StringComparison.OrdinalIgnoreCase))
        {
            return CreateActionResultInstance(Response<NoContent>.Success(204));
        }

        // BL-521 FIX5 item 9 — A TARGETED WRITE of the sign-in moment and the accepted invitation. It used to replace the
        // whole record at the loaded version on EVERY platform sign-in and refresh: it bumped the version (so an
        // administrator's open edit form was refused as "changed by another user" by a mere sign-in), stamped UpdatedBy
        // "auth-service", erased the pending-sync mark, and — refused by a concurrent edit — answered a 409 the caller could
        // not tell from a write that had landed. Now neither version, UpdatedBy nor mark changes, and nothing here can be
        // refused by a concurrent edit.
        var accepted = await _repository.RecordLoginAcceptedAsync(administrator.Id, DateTimeOffset.UtcNow, ct);
        return accepted
            ? CreateActionResultInstance(Response<NoContent>.Success(204))
            : CreateActionResultInstance(Response<NoContent>.Fail("Platform administrator not found or inactive.", 404));
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
        bool IsActive,
        string ActorType,
        IReadOnlyList<string> Roles);

    public sealed record PlatformAdministratorLoginAcceptedRequest(string Email);
}

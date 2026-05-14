using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class MfaChallengeService : IMfaChallengeService
{
    private const int ResendCooldownSeconds = 60;
    private const int MaxResendCount = 3;

    private readonly IMfaChallengeRepository _repository;
    private readonly IOtpDeliveryService _deliveryService;
    private readonly IUserRepository _userRepository;
    private readonly IAuthAuditService _authAuditService;
    private readonly ITenantContext _tenantContext;
    private readonly MfaOptions _options;

    public MfaChallengeService(
        IMfaChallengeRepository repository,
        IOtpDeliveryService deliveryService,
        IUserRepository userRepository,
        IAuthAuditService authAuditService,
        ITenantContext tenantContext,
        IOptions<MfaOptions> options)
    {
        _repository = repository;
        _deliveryService = deliveryService;
        _userRepository = userRepository;
        _authAuditService = authAuditService;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public async Task<MfaChallengeCreated> CreateEmailChallengeAsync(User user, string requestIp, string? userAgent, CancellationToken ct)
    {
        var challengeId = GenerateToken();
        var code = GenerateNumericCode(Math.Clamp(_options.OtpLength, 6, 10));
        var expiresAt = DateTime.UtcNow.AddMinutes(Math.Clamp(_options.ExpiryMinutes, 1, 15));

        var challenge = new MfaChallenge(
            _tenantContext.TenantId,
            user.Id,
            ComputeHash(challengeId),
            "email",
            ComputeHash(user.Email.Trim().ToLowerInvariant()),
            ComputeHash(code),
            expiresAt,
            Math.Clamp(_options.MaxAttempts, 1, 10),
            requestIp,
            userAgent);

        await _repository.CreateAsync(challenge, ct);
        await _deliveryService.SendEmailOtpAsync(user.Email, code, expiresAt, ct);

        return new MfaChallengeCreated(challengeId, MaskEmail(user.Email), "email", expiresAt);
    }

    public async Task<MfaChallengeCreated> ResendEmailChallengeAsync(string challengeId, string requestIp, string? userAgent, CancellationToken ct)
    {
        var challenge = await _repository.GetByChallengeIdHashAsync(ComputeHash(challengeId), ct);
        if (challenge is null || challenge.IsConsumed || challenge.IsExpired || !string.Equals(challenge.Channel, "email", StringComparison.OrdinalIgnoreCase))
        {
            await _authAuditService.WriteAsync("tenant_login_mfa_resend_requested", null, Guid.Empty, "{\"result\":\"rejected\"}", ct);
            throw new UnauthorizedAccessException("Verification challenge is not available.");
        }

        if (challenge.ResendCount >= MaxResendCount ||
            (challenge.LastResentAtUtc.HasValue && challenge.LastResentAtUtc.Value.AddSeconds(ResendCooldownSeconds) > DateTime.UtcNow))
        {
            await _authAuditService.WriteAsync("tenant_login_mfa_resend_requested", challenge.UserId, challenge.TenantId, "{\"result\":\"rate_limited\"}", ct);
            throw new UnauthorizedAccessException("Verification challenge is not available.");
        }

        var user = await _userRepository.GetByIdAndTenantAsync(challenge.UserId, challenge.TenantId, ct);
        if (user is null || !user.IsActive)
        {
            await _authAuditService.WriteAsync("tenant_login_mfa_resend_requested", challenge.UserId, challenge.TenantId, "{\"result\":\"user_unavailable\"}", ct);
            throw new UnauthorizedAccessException("Verification challenge is not available.");
        }

        var code = GenerateNumericCode(Math.Clamp(_options.OtpLength, 6, 10));
        var expiresAt = DateTime.UtcNow.AddMinutes(Math.Clamp(_options.ExpiryMinutes, 1, 15));
        challenge.RecordResend(ComputeHash(code), expiresAt);
        await _repository.UpdateAsync(challenge, ct);
        await _deliveryService.SendEmailOtpAsync(user.Email, code, expiresAt, ct);
        await _authAuditService.WriteAsync("tenant_login_mfa_resend_requested", user.Id, challenge.TenantId, "{\"result\":\"sent\",\"channel\":\"email\"}", ct);

        return new MfaChallengeCreated(challengeId, MaskEmail(user.Email), challenge.Channel, expiresAt);
    }

    public async Task<MfaChallenge> VerifyAsync(string challengeId, string code, CancellationToken ct)
    {
        var challenge = await _repository.GetByChallengeIdHashAsync(ComputeHash(challengeId), ct);
        if (challenge is null || challenge.IsConsumed || challenge.IsExpired || !challenge.HasAttemptsRemaining)
        {
            await _authAuditService.WriteAsync("tenant_login_mfa_verify_failed", challenge?.UserId, challenge?.TenantId ?? Guid.Empty, "{\"reason\":\"challenge_unavailable\"}", ct);
            throw new UnauthorizedAccessException("Invalid verification code.");
        }

        if (!FixedEquals(challenge.CodeHash, ComputeHash(code)))
        {
            challenge.RecordFailedAttempt();
            await _repository.UpdateAsync(challenge, ct);
            await _authAuditService.WriteAsync("tenant_login_mfa_verify_failed", challenge.UserId, challenge.TenantId, "{\"reason\":\"invalid_code\"}", ct);
            throw new UnauthorizedAccessException("Invalid verification code.");
        }

        challenge.Consume();
        await _repository.UpdateAsync(challenge, ct);
        return challenge;
    }

    private string ComputeHash(string value)
    {
        if (string.IsNullOrWhiteSpace(_options.HashSecret))
        {
            throw new InvalidOperationException("Mfa:HashSecret configuration is required when MFA challenges are used.");
        }

        var secret = _options.HashSecret;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateNumericCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        return RandomNumberGenerator.GetInt32(0, max).ToString($"D{length}");
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2 || parts[0].Length <= 2)
        {
            return "***";
        }

        return $"{parts[0][0]}***{parts[0][^1]}@{parts[1]}";
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// MOD-0022 E1 — tickets and KS-3 windows (pack §8 E1, CT-D1 / CT-D9).
///
/// <para>A ticket is 32 random bytes (base64url) returned once; only its HMAC-SHA256 is stored. Consumption is one
/// conditional update (<see cref="IESignReauthTicketRepository.TryConsumeAsync"/>), so two parallel consumers cannot
/// both win — the MFA challenge's read-then-replace pattern is deliberately NOT copied.</para>
/// </summary>
public sealed class ESignReauthTicketService : IESignReauthTicketService
{
    private const string HashPurpose = "diten.esign.reauth-ticket.v1:";
    private const int MinLifetimeSeconds = 30;
    private const int MaxLifetimeSeconds = 300;

    private readonly IESignReauthTicketRepository _tickets;
    private readonly IESignReauthWindowRepository _windows;
    private readonly IAuthAuditService _audit;
    private readonly byte[]? _key;
    private readonly TimeSpan _lifetime;

    public ESignReauthTicketService(
        IESignReauthTicketRepository tickets,
        IESignReauthWindowRepository windows,
        IAuthAuditService audit,
        IOptions<ESignOptions> options,
        IOptions<JwtSettings> jwtSettings,
        IHostEnvironment environment)
    {
        _tickets = tickets;
        _windows = windows;
        _audit = audit;

        _key = ResolveHashKey(options.Value.HashSecret, jwtSettings.Value.Secret, environment.IsDevelopment());
        _lifetime = TimeSpan.FromSeconds(Math.Clamp(options.Value.TicketLifetimeSeconds, MinLifetimeSeconds, MaxLifetimeSeconds));
    }

    public bool IsConfigured => _key is not null;

    /// <summary>
    /// FU-0022-12 (CT decision 2026-10-08): outside Development <c>ESign:HashSecret</c> is REQUIRED — there is no fallback
    /// to <c>JwtSettings:Secret</c>. A missing secret yields no key, so <see cref="IsConfigured"/> is false and every
    /// re-authentication answers 503 <c>ESIGN_REAUTH_UNAVAILABLE</c> (fail closed). Development keeps the fallback.
    /// </summary>
    public static byte[]? ResolveHashKey(string? hashSecret, string? jwtSecret, bool isDevelopment)
    {
        var secret = !string.IsNullOrWhiteSpace(hashSecret)
            ? hashSecret
            : isDevelopment ? jwtSecret : null;
        return string.IsNullOrWhiteSpace(secret) ? null : Encoding.UTF8.GetBytes(HashPurpose + secret);
    }

    public async Task<IssuedESignTicket> IssueAsync(
        Guid tenantId,
        Guid userId,
        string intent,
        ESignReauthKind kind,
        DateTime passwordVerifiedAtUtc,
        string requestIp,
        string? userAgent,
        CancellationToken ct)
    {
        var ticket = Base64Url(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.Add(_lifetime);
        await _tickets.CreateAsync(new ESignReauthTicket(
            tenantId, userId, Hash(ticket), intent, kind, passwordVerifiedAtUtc, expiresAt, requestIp, HashUserAgent(userAgent)), ct);
        return new IssuedESignTicket(ticket, expiresAt);
    }

    public async Task<DateTime> OpenWindowAsync(Guid tenantId, Guid userId, DateTime passwordVerifiedAtUtc, int minutes, string? userAgent, CancellationToken ct)
    {
        var expiresAt = passwordVerifiedAtUtc.AddMinutes(Math.Clamp(minutes, 1, ESignReauthWindow.MaxMinutes));
        await _windows.UpsertAsync(new ESignReauthWindow(tenantId, userId, HashUserAgent(userAgent), passwordVerifiedAtUtc, expiresAt), ct);
        return expiresAt;
    }

    public async Task<ESignReauthWindow?> GetOpenWindowAsync(Guid tenantId, Guid userId, string? userAgent, CancellationToken ct)
    {
        var window = await _windows.GetOpenAsync(tenantId, userId, DateTime.UtcNow, ct);
        return window is not null && FixedEquals(window.UserAgentHash, HashUserAgent(userAgent)) ? window : null;
    }

    public Task<ESignReauthTicket?> ConsumeAsync(string ticket, Guid tenantId, Guid userId, string intent, string consumedBy, CancellationToken ct) =>
        _tickets.TryConsumeAsync(Hash(ticket), tenantId, userId, intent, DateTime.UtcNow, consumedBy, ct);

    public async Task RevokeWindowsAsync(Guid tenantId, Guid userId, string reason, CancellationToken ct)
    {
        var revoked = await _windows.RevokeAllAsync(tenantId, userId, reason, DateTime.UtcNow, ct);
        if (revoked > 0)
        {
            await _audit.WriteAsync("tenant_esign_windows_revoked", userId, tenantId, JsonSerializer.Serialize(new { reason, revoked }), ct);
        }
    }

    private string Hash(string ticket)
    {
        var key = _key ?? throw new InvalidOperationException("E-signature ticket hashing is not configured.");
        return Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(ticket)));
    }

    private static string HashUserAgent(string? userAgent) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userAgent ?? string.Empty)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool FixedEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

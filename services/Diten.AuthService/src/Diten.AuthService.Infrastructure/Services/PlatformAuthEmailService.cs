using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// The platform administrator's set-password e-mail. BL-454 slice 2 stage D: in the shared shell, in the console's
/// language (en, tr — read from the request that caused it; English otherwise), and stating the expiry STORED with the
/// link (1 hour for "forgot password", 24 hours for the links an administrator issues), read back from the account the
/// caller has just written. The interface is unchanged: every caller writes the link first and sends second.
/// </summary>
public sealed class PlatformAuthEmailService : IPlatformAuthEmailService
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly SmtpOptions _smtpOptions;
    private readonly PlatformServiceOptions _platformOptions;
    private readonly IUserRepository? _users;
    private readonly IRefreshTokenHasher? _tokenHasher;
    private readonly IHttpContextAccessor? _httpContext;
    private readonly ILogger<PlatformAuthEmailService>? _logger;

    public PlatformAuthEmailService(
        IOptions<SmtpOptions> smtpOptions,
        IOptions<PlatformServiceOptions> platformOptions,
        IUserRepository? users = null,
        IRefreshTokenHasher? tokenHasher = null,
        IHttpContextAccessor? httpContext = null,
        ILogger<PlatformAuthEmailService>? logger = null)
    {
        _smtpOptions = smtpOptions.Value;
        _platformOptions = platformOptions.Value;
        _users = users;
        _tokenHasher = tokenHasher;
        _httpContext = httpContext;
        _logger = logger;
    }

    public async Task SendPlatformPasswordResetAsync(string email, string resetToken, CancellationToken ct)
    {
        ValidateSmtpConfiguration();
        using var message = await BuildMessageAsync(email, resetToken, ct);

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password)
        };

        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, ct);
    }

    /// <summary>The whole message, short of sending it — what a test reads to see what would leave.</summary>
    public async Task<MailMessage> BuildMessageAsync(string email, string resetToken, CancellationToken ct)
    {
        var language = PlatformPasswordResetEmailTemplate.LanguageOf(RequestLanguage());
        var expiresAtUtc = await StoredExpiryAsync(email, resetToken, ct);
        var rendered = PlatformPasswordResetEmailTemplate.Render(language, BuildPlatformPasswordResetUrl(email, resetToken), expiresAtUtc);

        var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, Diten.BuildingBlocks.Email.EmailProduct.Name, Encoding.UTF8),
            Subject = PlatformPasswordResetEmailTemplate.Subject(language),
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = rendered.Text
        };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(rendered.Html, Encoding.UTF8, MediaTypeNames.Text.Html));
        message.To.Add(email);
        return message;
    }

    public string BuildPlatformPasswordResetUrl(string email, string resetToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_platformOptions.FrontendBaseUrl)
            ? "http://localhost:5001"
            : _platformOptions.FrontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/platform/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(resetToken)}";
    }

    // The expiry written with THIS token (the hash must match: never another link's time). Null when it cannot be read —
    // the mail then says "can be used once" without a time rather than a wrong one.
    private async Task<DateTime?> StoredExpiryAsync(string email, string resetToken, CancellationToken ct)
    {
        if (_users is null || _tokenHasher is null)
        {
            return null;
        }

        try
        {
            var user = await _users.GetByEmailAndTenantAsync(email.Trim().ToLowerInvariant(), PlatformTenantId, ct);
            return user is not null
                   && user.PasswordResetTokenExpiresAt is { } expiresAt
                   && string.Equals(user.PasswordResetTokenHash, _tokenHasher.Hash(resetToken), StringComparison.Ordinal)
                ? DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)
                : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "platform.password_link.expiry_unavailable");
            return null;
        }
    }

    // The browser that asked ("forgot password") or the console that acted; Platform's server-to-server calls carry none.
    private string? RequestLanguage()
    {
        var header = _httpContext?.HttpContext?.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var first = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return first?.Split(';')[0];
    }

    private void ValidateSmtpConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host) ||
            string.IsNullOrWhiteSpace(_smtpOptions.FromEmail))
        {
            throw new InvalidOperationException("SMTP configuration is incomplete.");
        }
    }
}

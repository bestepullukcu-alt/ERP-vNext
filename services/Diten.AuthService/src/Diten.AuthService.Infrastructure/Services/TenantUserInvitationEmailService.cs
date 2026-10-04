using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

// Tenant-user invitation email (mirrors PlatformAuthEmailService). Sends a "set your password"
// link pointing at the tenant set-password page on the frontend shell.
//
// BL-454 — the mail now goes out in the shared e-mail shell, under the tenant's name ("{tenant} (via Di10)"), in
// the tenant's language, with the tenant's reply address. The interface is unchanged on purpose: its callers hand
// over an address and a token, and everything else is resolved HERE — the tenant from the request's own tenant
// context (an administrator invites into the tenant they are acting in), the reader's first name from the user
// record that was just written, the tenant's name/language/reply address from Platform. Each of those lookups may
// fail without failing the invitation: no name → no greeting; no answer from Platform → the product's name, in
// English.
public sealed class TenantUserInvitationEmailService : ITenantUserInvitationEmailService
{
    private readonly SmtpOptions _smtpOptions;
    private readonly PlatformServiceOptions _platformOptions;
    private readonly ITenantContext _tenantContext;
    private readonly IUserRepository _users;
    private readonly ITenantEmailIdentityClient _identity;
    private readonly ILogger<TenantUserInvitationEmailService> _logger;

    public TenantUserInvitationEmailService(
        IOptions<SmtpOptions> smtpOptions,
        IOptions<PlatformServiceOptions> platformOptions,
        ITenantContext tenantContext,
        IUserRepository users,
        ITenantEmailIdentityClient identity,
        ILogger<TenantUserInvitationEmailService> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _platformOptions = platformOptions.Value;
        _tenantContext = tenantContext;
        _users = users;
        _identity = identity;
        _logger = logger;
    }

    public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct) =>
        SendAsync(email, setupToken, isPasswordReset: false, ct);

    public Task SendTenantUserPasswordResetAsync(string email, string setupToken, CancellationToken ct) =>
        SendAsync(email, setupToken, isPasswordReset: true, ct);

    private async Task SendAsync(string email, string setupToken, bool isPasswordReset, CancellationToken ct)
    {
        ValidateSmtpConfiguration();

        using var message = await BuildMessageAsync(email, setupToken, ct, isPasswordReset);

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password)
        };

        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, ct);
    }

    /// <summary>The whole message, short of sending it — what a test reads to see what would leave.</summary>
    /// <param name="isPasswordReset">What the CALLER is sending: an administrator's reset (true) or an invitation.
    /// Never inferred from the user record — a record that cannot be read must not change the words.</param>
    public async Task<MailMessage> BuildMessageAsync(string email, string setupToken, CancellationToken ct, bool isPasswordReset = false)
    {
        var tenantId = ResolveTenantId();
        var identity = tenantId == Guid.Empty ? null : await _identity.GetAsync(tenantId, ct);
        // BL-454 — the recipient is ONE plain address or nothing is sent: a value with a line break or a second
        // mailbox never reaches a header. Coded, and thrown before anything is built.
        if (!EmailAddressText.IsSingleAddress(email?.Trim()))
        {
            throw new InvalidOperationException("RECIPIENT_INVALID");
        }

        var user = await ResolveUserAsync(email!, tenantId, ct);
        var firstName = string.IsNullOrWhiteSpace(user?.FirstName) ? null : user.FirstName.Trim();
        var replyTo = EmailAddressText.IsSingleAddress(identity?.ReplyToEmail?.Trim()) ? identity!.ReplyToEmail!.Trim() : null;
        if (replyTo is null && !string.IsNullOrWhiteSpace(identity?.ReplyToEmail))
        {
            _logger.LogWarning("tenant.invitation.reply_to_refused TenantId={TenantId}. The reply address is not a single valid address.", tenantId);
        }

        var language = identity?.Language;
        var rendered = TenantUserInvitationEmailTemplate.Render(
            language, identity?.DisplayName, firstName, BuildTenantSetPasswordUrl(email!, setupToken), replyTo, isPasswordReset);

        var message = new MailMessage
        {
            From = new MailAddress(
                _smtpOptions.FromEmail,
                EmailSender.ComposeDisplayName(identity?.SenderName, identity?.DisplayName, language),
                Encoding.UTF8),
            Subject = TenantUserInvitationEmailTemplate.Subject(language, identity?.DisplayName, isPasswordReset),
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            // The plain-text part is the body; the framed HTML is its alternative — a client shows the richest it can.
            IsBodyHtml = false,
            Body = rendered.Text
        };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(rendered.Html, Encoding.UTF8, MediaTypeNames.Text.Html));
        message.To.Add(new MailAddress(email!.Trim()));

        if (replyTo is not null)
        {
            message.ReplyToList.Add(new MailAddress(replyTo));
        }

        return message;
    }

    public string BuildTenantSetPasswordUrl(string email, string setupToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_platformOptions.FrontendBaseUrl)
            ? "http://localhost:5001"
            : _platformOptions.FrontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/account/set-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(setupToken)}";
    }

    // A platform administrator acting on a tenant carries that tenant as the TARGET; everyone else is in their own.
    private Guid ResolveTenantId()
    {
        if (_tenantContext.IsPlatformContext)
        {
            return _tenantContext.TargetTenantId ?? Guid.Empty;
        }

        return _tenantContext.IsResolved ? _tenantContext.TenantId : Guid.Empty;
    }

    private async Task<Diten.AuthService.Domain.Entities.User?> ResolveUserAsync(string email, Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        try
        {
            return await _users.GetByEmailAndTenantAsync(email, tenantId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "tenant.invitation.recipient_unavailable TenantId={TenantId}", tenantId);
            return null;
        }
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

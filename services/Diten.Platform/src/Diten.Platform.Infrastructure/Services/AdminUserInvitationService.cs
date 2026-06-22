using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Services.EmailTemplates;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

public sealed class AdminUserInvitationService : IAdminUserInvitationService
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SmtpOptions _smtpOptions;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly ILogger<AdminUserInvitationService> _logger;

    public AdminUserInvitationService(
        IHttpClientFactory httpClientFactory,
        IOptions<SmtpOptions> smtpOptions,
        IOptions<AuthServiceOptions> authServiceOptions,
        ILogger<AdminUserInvitationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _smtpOptions = smtpOptions.Value;
        _authServiceOptions = authServiceOptions.Value;
        _logger = logger;
    }

    public async Task<AdminUserInvitationResult> InviteAsync(Tenant tenant, TenantAdminUser adminUser, CancellationToken cancellationToken)
    {
        var provisioned = await ProvisionAdminUserAsync(tenant, adminUser, cancellationToken);
        var loginUrl = BuildLoginUrl(tenant);

        // When SMTP is not configured (typical dev default), skip the email instead of throwing so the
        // provisioned admin (already created with a temp password) is not lost behind a 502. The handler
        // surfaces the login URL + temp password to the operator in Development.
        var emailSent = false;
        if (IsSmtpConfigured())
        {
            await SendInvitationEmailAsync(tenant, adminUser, loginUrl, provisioned.TemporaryPassword, cancellationToken);
            emailSent = true;
        }
        else
        {
            _logger.LogWarning(
                "SMTP is not configured; skipping admin invitation email. TenantId={TenantId} AdminUserId={AdminUserId}",
                tenant.Id,
                adminUser.Id);
        }

        return new AdminUserInvitationResult(
            loginUrl,
            provisioned.TemporaryPassword,
            provisioned.UserProvisioned,
            InvitationEmailSent: emailSent);
    }

    private async Task<AdminProvisioningResponse> ProvisionAdminUserAsync(Tenant tenant, TenantAdminUser adminUser, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl))
        {
            throw new InvalidOperationException("AuthService:BaseUrl configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            throw new InvalidOperationException("AuthService:InternalApiKey configuration is required.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_authServiceOptions.BaseUrl.TrimEnd('/')}/internal/events/tenant-admin-invited")
        {
            Content = JsonContent.Create(new AdminProvisioningRequest(
                tenant.Id,
                adminUser.Id,
                tenant.Code,
                tenant.DisplayName ?? tenant.Name,
                adminUser.Email,
                adminUser.Name))
        };
        request.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<AdminProvisioningResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.TemporaryPassword))
        {
            var responseText = payload?.Message ?? await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Tenant admin provisioning failed. TenantId={TenantId} AdminUserId={AdminUserId} StatusCode={StatusCode} Response={Response}",
                tenant.Id,
                adminUser.Id,
                (int)response.StatusCode,
                responseText);
            throw new InvalidOperationException("Admin user provisioning failed.");
        }

        return payload;
    }

    private async Task SendInvitationEmailAsync(
        Tenant tenant,
        TenantAdminUser adminUser,
        string loginUrl,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = AdminUserInvitationEmailTemplate.Subject(tenant),
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            IsBodyHtml = true,
            Body = AdminUserInvitationEmailTemplate.Render(tenant, adminUser, loginUrl, temporaryPassword)
        };

        message.To.Add(adminUser.Email);

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }

    private bool IsSmtpConfigured() =>
        _smtpOptions.Enabled &&
        !string.IsNullOrWhiteSpace(_smtpOptions.Host) &&
        !string.IsNullOrWhiteSpace(_smtpOptions.Username) &&
        !string.IsNullOrWhiteSpace(_smtpOptions.Password) &&
        !string.IsNullOrWhiteSpace(_smtpOptions.FromEmail);

    private string BuildLoginUrl(Tenant tenant)
    {
        var template = string.IsNullOrWhiteSpace(_authServiceOptions.TenantLoginUrlTemplate)
            ? "https://{tenantDomain}/account/login?tenantId={tenantId}"
            : _authServiceOptions.TenantLoginUrlTemplate;

        var tenantDomain = NormalizeTenantDomain(tenant.Domain);

        return template
            .Replace("{tenantDomain}", tenantDomain, StringComparison.OrdinalIgnoreCase)
            .Replace("{tenantId}", tenant.Id.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{tenantSlug}", tenant.Slug, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTenantDomain(string domain)
    {
        var normalized = (domain ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Tenant domain is required for invitation login URL.");
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return normalized.TrimEnd('/');
    }

    private sealed record AdminProvisioningRequest(
        Guid TenantId,
        Guid AdminUserId,
        string TenantCode,
        string TenantName,
        string Email,
        string Name);

    private sealed record AdminProvisioningResponse(
        bool UserProvisioned,
        string TemporaryPassword,
        string? Message);
}

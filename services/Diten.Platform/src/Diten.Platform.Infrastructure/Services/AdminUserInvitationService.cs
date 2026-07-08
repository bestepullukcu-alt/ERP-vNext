using System.Net.Http.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

public sealed class AdminUserInvitationService : IAdminUserInvitationService
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    // MOD-0027 platform-default template that carries the invite layout (subject/body/variables).
    private const string InvitationTemplateKey = "tenant.invite.email";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediator _mediator;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly ILogger<AdminUserInvitationService> _logger;

    public AdminUserInvitationService(
        IHttpClientFactory httpClientFactory,
        IMediator mediator,
        IOptions<AuthServiceOptions> authServiceOptions,
        ILogger<AdminUserInvitationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _mediator = mediator;
        _authServiceOptions = authServiceOptions.Value;
        _logger = logger;
    }

    public async Task<AdminUserInvitationResult> InviteAsync(Tenant tenant, TenantAdminUser adminUser, CancellationToken cancellationToken)
    {
        var provisioned = await ProvisionAdminUserAsync(tenant, adminUser, cancellationToken);
        var loginUrl = BuildLoginUrl(tenant);

        // The invitation email is delivered through the MOD-0027 notification pipeline: it renders the
        // tenant.invite.email template, resolves the tenant's messaging settings (tenant-specific -> platform
        // default -> fallback policy) and records a dispatch for monitoring. If no provider/settings resolve
        // (typical dev default), the send fails gracefully: the admin stays provisioned (with a temp password)
        // and the handler surfaces the login URL + temp password to the operator in Development.
        var emailSent = await TryQueueInvitationEmailAsync(tenant, adminUser, loginUrl, provisioned.TemporaryPassword, cancellationToken);

        return new AdminUserInvitationResult(
            loginUrl,
            provisioned.TemporaryPassword,
            provisioned.UserProvisioned,
            InvitationEmailSent: emailSent);
    }

    private async Task<bool> TryQueueInvitationEmailAsync(
        Tenant tenant,
        TenantAdminUser adminUser,
        string loginUrl,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        // TemporaryPassword is intentionally passed as a template variable: the notification handler renders it
        // into the sent body but persists a redacted preview (sensitive keys/values are masked), so the secret
        // never lands in the dispatch record or the monitoring UI.
        var request = new QueueEmailNotificationRequest(
            InvitationTemplateKey,
            string.IsNullOrWhiteSpace(tenant.DefaultLanguage) ? "en" : tenant.DefaultLanguage,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["RecipientName"] = string.IsNullOrWhiteSpace(adminUser.Name) ? adminUser.Email : adminUser.Name,
                ["TenantDisplayName"] = tenant.DisplayName ?? tenant.Name,
                ["Email"] = adminUser.Email,
                ["TemporaryPassword"] = temporaryPassword,
                ["LoginUrl"] = loginUrl
            },
            new[] { new EmailRecipientDto(adminUser.Email, adminUser.Name) });

        try
        {
            var response = await _mediator.Send(new QueueEmailNotificationCommand(tenant.Id, request), cancellationToken);
            if (!response.IsSuccessful)
            {
                _logger.LogWarning(
                    "Admin invitation notification was not delivered. TenantId={TenantId} AdminUserId={AdminUserId} StatusCode={StatusCode} Errors={Errors}",
                    tenant.Id,
                    adminUser.Id,
                    response.StatusCode,
                    string.Join("; ", response.Errors));
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Provisioning already succeeded; a notification failure must not fail the whole invite.
            _logger.LogWarning(
                ex,
                "Admin invitation notification queue failed. TenantId={TenantId} AdminUserId={AdminUserId}",
                tenant.Id,
                adminUser.Id);
            return false;
        }
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

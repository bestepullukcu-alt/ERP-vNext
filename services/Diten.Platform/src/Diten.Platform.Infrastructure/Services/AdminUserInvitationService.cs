using System.Globalization;
using System.Net.Http.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

public sealed class AdminUserInvitationService : IAdminUserInvitationService
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    /// <summary>
    /// BL-454 — the client that carries the internal API key to AuthService: <see cref="InternalHttpClients.AuthInternal"/>,
    /// which never follows a redirect — the key must not travel to whatever host a 3xx names.
    /// </summary>
    public const string AuthInternalClientName = InternalHttpClients.AuthInternal;

    // MOD-0027-FU04C — the invite is dispatched by canonical eventCode (FU04A tenant.user.invited, bound to the
    // tenant.invite.email template) through the FU04B EventCode Dispatch Adapter, not by a raw templateKey.
    private const string InvitationEventCode = "tenant.user.invited";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediator _mediator;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly IHostEnvironment? _environment;
    private readonly ILogger<AdminUserInvitationService> _logger;

    public AdminUserInvitationService(
        IHttpClientFactory httpClientFactory,
        IMediator mediator,
        IOptions<AuthServiceOptions> authServiceOptions,
        ILogger<AdminUserInvitationService> logger,
        IHostEnvironment? environment = null)
    {
        _httpClientFactory = httpClientFactory;
        _mediator = mediator;
        _authServiceOptions = authServiceOptions.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task<AdminUserInvitationResult> InviteAsync(Tenant tenant, TenantAdminUser adminUser, CancellationToken cancellationToken)
    {
        var provisioned = await ProvisionAdminUserAsync(tenant, adminUser, cancellationToken);
        var loginUrl = BuildLoginUrl(tenant);

        // BL-454 slice 2 stage D — the invitation carries BL-529's one-time set-password link and how long it lives;
        // never a password (AuthService no longer makes one). Delivered through the MOD-0027 notification pipeline
        // (tenant.invite.email, the tenant's messaging settings, a dispatch row whose stored variables and previews mask
        // the link). A link that would point at localhost outside Development is not sent at all, by name.
        var (setPasswordUrl, refusal) = TenantAdminSetPasswordLink.Build(
            _authServiceOptions.FrontendBaseUrl, _environment?.IsDevelopment() == true, adminUser.Email, provisioned.SetupToken!);
        if (refusal is not null)
        {
            _logger.LogWarning(
                "tenant.admin_invitation.not_sent TenantId={TenantId} AdminUserId={AdminUserId} ReasonCode={ReasonCode}",
                tenant.Id, adminUser.Id, refusal);
            return new AdminUserInvitationResult(loginUrl, null, provisioned.UserProvisioned, InvitationEmailSent: false, EmailRefusalCode: refusal);
        }

        var emailSent = await TryQueueInvitationEmailAsync(tenant, adminUser, setPasswordUrl!, provisioned.SetupExpiresAtUtc, cancellationToken);

        return new AdminUserInvitationResult(
            loginUrl,
            setPasswordUrl,
            provisioned.UserProvisioned,
            InvitationEmailSent: emailSent);
    }

    /// <summary>The variables of tenant.invite.email (1.2.0) — the same in every language.</summary>
    internal static Dictionary<string, object?> InvitationVariables(
        Tenant tenant, TenantAdminUser adminUser, string setPasswordUrl, DateTime setupExpiresAtUtc) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RecipientName"] = string.IsNullOrWhiteSpace(adminUser.Name) ? adminUser.Email : adminUser.Name,
            ["TenantDisplayName"] = tenant.DisplayName ?? tenant.Name,
            ["TenantId"] = tenant.Id,
            ["Email"] = adminUser.Email,
            // A secret NAME (NotificationSecrets): the sent body carries it, the dispatch row never does.
            ["SetPasswordUrl"] = setPasswordUrl,
            ["LinkExpiresAtUtc"] = DateTime.SpecifyKind(setupExpiresAtUtc, DateTimeKind.Utc)
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        };

    private async Task<bool> TryQueueInvitationEmailAsync(
        Tenant tenant,
        TenantAdminUser adminUser,
        string setPasswordUrl,
        DateTime setupExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        var dispatchRequest = new NotificationEventDispatchRequest(
            tenant.Id,
            InvitationEventCode,
            new[] { new EmailRecipientDto(adminUser.Email, adminUser.Name) },
            InvitationVariables(tenant, adminUser, setPasswordUrl, setupExpiresAtUtc),
            Locale: string.IsNullOrWhiteSpace(tenant.DefaultLanguage) ? "en" : tenant.DefaultLanguage);

        try
        {
            // Fail-soft: provisioning already succeeded; a notification failure must NOT fail the invite.
            var response = await _mediator.Send(new DispatchNotificationByEventCodeCommand(dispatchRequest), cancellationToken);
            if (!response.IsSuccessful)
            {
                _logger.LogWarning(
                    "Admin invitation notification was not delivered. TenantId={TenantId} AdminUserId={AdminUserId} EventCode={EventCode} StatusCode={StatusCode} ReasonCode={ReasonCode} Errors={Errors}",
                    tenant.Id,
                    adminUser.Id,
                    InvitationEventCode,
                    response.StatusCode,
                    response.ReasonCode,
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

        var client = _httpClientFactory.CreateClient(AuthInternalClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<AdminProvisioningResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.SetupToken))
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

    /// <summary>AuthService's answer: the one-time set-password token (in clear only here, over the internal key) and
    /// when it stops working. No password.</summary>
    private sealed record AdminProvisioningResponse(
        bool UserProvisioned,
        string? SetupToken,
        DateTime SetupExpiresAtUtc,
        string? Message);
}

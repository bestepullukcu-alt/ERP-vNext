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

    public async Task<AdminUserInvitationResult> InviteAsync(
        Tenant tenant, TenantAdminUser adminUser, AdminInvitationTrigger trigger, CancellationToken cancellationToken)
    {
        var loginUrl = BuildLoginUrl(tenant);
        var isDevelopment = _environment?.IsDevelopment() == true;

        // BL-454 stage D FIX1 (2) — the root first, AuthService second: a link that may not be sent from this server must not
        // cost an existing administrator their password and sessions (the operator path resets) for a mail that never leaves.
        if (TenantAdminSetPasswordLink.RefusalFor(_authServiceOptions.FrontendBaseUrl, isDevelopment) is { } rootRefusal)
        {
            _logger.LogWarning(
                "tenant.admin_invitation.not_sent TenantId={TenantId} AdminUserId={AdminUserId} Trigger={Trigger} ReasonCode={ReasonCode}. AuthService was not called.",
                tenant.Id, adminUser.Id, trigger, rootRefusal);
            return new AdminUserInvitationResult(loginUrl, null, UserProvisioned: false, InvitationEmailSent: false, EmailRefusalCode: rootRefusal);
        }

        var provisioned = await ProvisionAdminUserAsync(tenant, adminUser, trigger, cancellationToken);
        if (provisioned.RefusalCode is { } authRefusal)
        {
            _logger.LogWarning(
                "tenant.admin_invitation.not_sent TenantId={TenantId} AdminUserId={AdminUserId} Trigger={Trigger} ReasonCode={ReasonCode}",
                tenant.Id, adminUser.Id, trigger, authRefusal);
            return new AdminUserInvitationResult(loginUrl, null, UserProvisioned: false, InvitationEmailSent: false, EmailRefusalCode: authRefusal);
        }

        if (provisioned.SetupToken is null)
        {
            // The event path over an account that already exists: AuthService changed nothing, and nothing is sent.
            _logger.LogInformation(
                "tenant.admin_invitation.account_exists TenantId={TenantId} AdminUserId={AdminUserId} Trigger={Trigger}",
                tenant.Id, adminUser.Id, trigger);
            return new AdminUserInvitationResult(loginUrl, null, UserProvisioned: false, InvitationEmailSent: false,
                EmailRefusalCode: AdminInvitationRefusals.AccountExists);
        }

        // BL-454 slice 2 stage D — the invitation carries BL-529's one-time set-password link and how long it lives;
        // never a password (AuthService no longer makes one). Delivered through the MOD-0027 notification pipeline
        // (tenant.invite.email, the tenant's messaging settings, a dispatch row whose stored variables and previews mask
        // the link). A link that would point at localhost outside Development is not sent at all, by name.
        var (setPasswordUrl, refusal) = TenantAdminSetPasswordLink.Build(
            _authServiceOptions.FrontendBaseUrl, isDevelopment, adminUser.Email, provisioned.SetupToken);
        if (refusal is not null)
        {
            _logger.LogWarning(
                "tenant.admin_invitation.not_sent TenantId={TenantId} AdminUserId={AdminUserId} ReasonCode={ReasonCode}",
                tenant.Id, adminUser.Id, refusal);
            return new AdminUserInvitationResult(loginUrl, null, provisioned.UserProvisioned, InvitationEmailSent: false, EmailRefusalCode: refusal);
        }

        var (emailSent, dispatchId) = await TryQueueInvitationEmailAsync(tenant, adminUser, setPasswordUrl!, provisioned.SetupExpiresAtUtc!.Value, cancellationToken);

        return new AdminUserInvitationResult(
            loginUrl,
            setPasswordUrl,
            provisioned.UserProvisioned,
            InvitationEmailSent: emailSent,
            InvitationDispatchId: dispatchId);
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

    /// <returns>Whether the e-mail was queued, and the dispatch that carries it (when the pipeline names one).</returns>
    private async Task<(bool Sent, Guid? DispatchId)> TryQueueInvitationEmailAsync(
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
                return (false, null);
            }

            return (true, response.Data?.Id is { } id && id != Guid.Empty ? id : null);
        }
        catch (Exception ex)
        {
            // Provisioning already succeeded; a notification failure must not fail the whole invite.
            _logger.LogWarning(
                ex,
                "Admin invitation notification queue failed. TenantId={TenantId} AdminUserId={AdminUserId}",
                tenant.Id,
                adminUser.Id);
            return (false, null);
        }
    }

    /// <inheritdoc />
    public string? LinkRootRefusal() =>
        TenantAdminSetPasswordLink.RefusalFor(_authServiceOptions.FrontendBaseUrl, _environment?.IsDevelopment() == true);

    /// <summary>The event's door (create only, by construction) and the operator's door (resets an existing account).</summary>
    internal const string CreateOnlyPath = "/internal/events/tenant-admin-created";
    internal const string OperatorPath = "/internal/events/tenant-admin-invited";

    private async Task<AdminProvisioningResponse> ProvisionAdminUserAsync(
        Tenant tenant, TenantAdminUser adminUser, AdminInvitationTrigger trigger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl))
        {
            throw new InvalidOperationException("AuthService:BaseUrl configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            throw new InvalidOperationException("AuthService:InternalApiKey configuration is required.");
        }

        // BL-454 stage D FIX2 (3) — the operator's "Invite" goes to the operator's door (it resets an existing account,
        // audited); everything else — the tenant-created event, and an unset trigger (K8) — to the create-only door. An
        // AuthService that does not know that door answers 404: this call fails closed (throws, the transport retries) and
        // never falls back to the operator's door.
        var operatorInvite = trigger == AdminInvitationTrigger.Operator;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _authServiceOptions.BaseUrl.TrimEnd('/') + (operatorInvite ? OperatorPath : CreateOnlyPath))
        {
            Content = JsonContent.Create(new AdminProvisioningRequest(
                tenant.Id,
                adminUser.Id,
                tenant.Code,
                tenant.DisplayName ?? tenant.Name,
                adminUser.Email,
                adminUser.Name,
                operatorInvite ? TriggerOperatorInvite : null))
        };
        request.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);

        var client = _httpClientFactory.CreateClient(AuthInternalClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        // K11 — AuthService refuses the platform's own tenant by name: told to the operator by that name, not as a 502.
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest
            && text.Contains(AdminInvitationRefusals.PlatformTenantRefused, StringComparison.Ordinal))
        {
            return new AdminProvisioningResponse(false, null, null, null) { RefusalCode = AdminInvitationRefusals.PlatformTenantRefused };
        }

        AdminProvisioningResponse? payload = null;
        if (response.IsSuccessStatusCode)
        {
            try
            {
                payload = System.Text.Json.JsonSerializer.Deserialize<AdminProvisioningResponse>(text, JsonOptions);
            }
            catch (System.Text.Json.JsonException)
            {
                payload = null;
            }
        }

        // BL-454 stage D FIX3 (2) — a 2xx body may carry the new administrator's live set-password token: it is NEVER logged,
        // only the status and a fixed reason. A non-2xx body is logged shortened, with every token-like value masked.
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Tenant admin provisioning failed. TenantId={TenantId} AdminUserId={AdminUserId} StatusCode={StatusCode} Response={Response}",
                tenant.Id, adminUser.Id, (int)response.StatusCode, LoggableErrorBody(text));
            throw new InvalidOperationException("Admin user provisioning failed.");
        }

        if (payload is null)
        {
            _logger.LogError(
                "Tenant admin provisioning failed. TenantId={TenantId} AdminUserId={AdminUserId} StatusCode={StatusCode} ReasonCode={ReasonCode}",
                tenant.Id, adminUser.Id, (int)response.StatusCode, ReasonAuthAnswerUnreadable);
            throw new InvalidOperationException("Admin user provisioning failed.");
        }

        // E5 — no token is "the account exists" ONLY when AuthService says exactly that; any other token-less answer (or a
        // token without its expiry) is not trusted: named, and nothing is sent.
        var accountExists = payload.SetupToken is null && string.Equals(payload.Message, StatusAccountExists, StringComparison.Ordinal);
        if (!accountExists && (string.IsNullOrWhiteSpace(payload.SetupToken) || payload.SetupExpiresAtUtc is null))
        {
            _logger.LogError(
                "tenant.admin_invitation.auth_answer_invalid TenantId={TenantId} AdminUserId={AdminUserId} HasToken={HasToken} HasExpiry={HasExpiry}",
                tenant.Id, adminUser.Id, !string.IsNullOrWhiteSpace(payload.SetupToken), payload.SetupExpiresAtUtc is not null);
            return payload with { SetupToken = null, RefusalCode = AdminInvitationRefusals.AuthAnswerInvalid };
        }

        return payload;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>A 2xx answer that could not be read (its body is never logged: it may hold a live token).</summary>
    internal const string ReasonAuthAnswerUnreadable = "AUTH_ANSWER_UNREADABLE";

    /// <summary>The most of a non-2xx body that is logged.</summary>
    internal const int MaxLoggedBodyLength = 256;

    // Any key with "token" in its name, as JSON ("setupToken": "…") or as a pair (token=…): its value is masked.
    private static readonly System.Text.RegularExpressions.Regex TokenJsonValue = new(
        "(\"[^\"]*token[^\"]*\"\\s*:\\s*)(\"(?:[^\"\\\\]|\\\\.)*\"|[^,}\\s]+)",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly System.Text.RegularExpressions.Regex TokenPairValue = new(
        "(\\b\\w*token\\w*\\s*=\\s*)[^\\s&,;]+",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>A non-2xx body as it may be logged: every token-like value masked FIRST, then shortened.</summary>
    internal static string LoggableErrorBody(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        var masked = TokenPairValue.Replace(TokenJsonValue.Replace(body, "$1\"***\""), "$1***");
        return masked.Length <= MaxLoggedBodyLength ? masked : masked[..MaxLoggedBodyLength] + "…";
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
        string Name,
        string? Trigger);

    // AuthService's InternalEventsController names (the internal contract).
    private const string TriggerOperatorInvite = "operator-invite";
    private const string StatusAccountExists = "account_exists";

    /// <summary>AuthService's answer: the one-time set-password token (in clear only here, over the internal key) and
    /// when it stops working. No password.</summary>
    private sealed record AdminProvisioningResponse(
        bool UserProvisioned,
        string? SetupToken,
        DateTime? SetupExpiresAtUtc,
        string? Message)
    {
        /// <summary>Set by this service (never read from the wire): a named reason nothing may be sent.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string? RefusalCode { get; init; }
    }
}

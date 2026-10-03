using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Commands;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceIdentityTokenHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Legacy_missing_or_empty_purpose_remains_audit_only(string? legacyPurpose)
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = Identity(verifier, "Diten.MDM", legacyPurpose);
        var grants = new GrantRepository(true);
        var handler = Handler(identity, grants, verifier);
        var tenantId = Guid.NewGuid();

        var response = await handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", tenantId, "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(tenantId, grants.TenantId);
        Assert.Equal(identity.Id, grants.ClientId);
        Assert.Equal("TRUSTED_AUDIT_SOURCE_INGEST", grants.Audience);
    }

    [Fact]
    public async Task Same_identity_cannot_issue_both_audiences_even_when_grant_exists()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var auditIdentity = Identity(verifier, "Diten.MDM");
        var workflowIdentity = Identity(verifier, "Diten.MDM", ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer);

        var workflowFromAudit = await Handler(auditIdentity, new GrantRepository(true), verifier).Handle(
            new IssueServiceIdentityTokenCommand("mdm", "secret", Guid.NewGuid(), ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer),
            CancellationToken.None);
        var auditFromWorkflow = await Handler(workflowIdentity, new GrantRepository(true), verifier).Handle(
            new IssueServiceIdentityTokenCommand("mdm", "secret", Guid.NewGuid(), ServiceIdentityTokenAudiencePolicy.TrustedAuditSourceIngest),
            CancellationToken.None);

        Assert.Equal(403, workflowFromAudit.StatusCode);
        Assert.Equal(403, auditFromWorkflow.StatusCode);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("OTHER_AUDIENCE")]
    [InlineData("trusted_workflow_consumer")]
    public async Task Malformed_persisted_identity_purpose_is_state_conflict(string purpose)
    {
        var verifier = new ServiceClientCredentialVerifier();
        var response = await Handler(Identity(verifier, "Diten.MDM", purpose), new GrantRepository(true), verifier).Handle(
            new IssueServiceIdentityTokenCommand("mdm", "secret", Guid.NewGuid(), ServiceIdentityTokenAudiencePolicy.TrustedAuditSourceIngest),
            CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Dedicated_workflow_client_with_enabled_exact_grant_succeeds()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = Identity(verifier, "Diten.MDM", ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer);
        var grants = new GrantRepository(true);
        var handler = Handler(identity, grants, verifier);
        var tenantId = Guid.NewGuid();

        var response = await handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", tenantId, ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(tenantId, grants.TenantId);
        Assert.Equal(identity.Id, grants.ClientId);
        Assert.Equal(ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer, grants.Audience);
    }

    [Fact]
    public async Task Dedicated_reference_data_client_with_enabled_exact_grant_succeeds()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = Identity(verifier, "Diten.MDM", ServiceIdentityTokenAudiencePolicy.TrustedReferenceDataConsumer);
        var grants = new GrantRepository(true);
        var tenantId = Guid.NewGuid();

        var response = await Handler(identity, grants, verifier).Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", tenantId, ServiceIdentityTokenAudiencePolicy.TrustedReferenceDataConsumer), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(tenantId, grants.TenantId);
        Assert.Equal(identity.Id, grants.ClientId);
        Assert.Equal(ServiceIdentityTokenAudiencePolicy.TrustedReferenceDataConsumer, grants.Audience);
    }

    [Theory]
    [InlineData("TRUSTED_AUDIT_SOURCE_INGEST")]
    [InlineData("TRUSTED_WORKFLOW_CONSUMER")]
    public async Task Dedicated_reference_data_identity_cannot_issue_other_purposes(string audience)
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = Identity(verifier, "Diten.MDM", ServiceIdentityTokenAudiencePolicy.TrustedReferenceDataConsumer);

        var response = await Handler(identity, new GrantRepository(true), verifier).Handle(
            new IssueServiceIdentityTokenCommand("mdm", "secret", Guid.NewGuid(), audience), CancellationToken.None);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task Dedicated_reference_data_identity_with_wrong_credential_is_unauthorized()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = Identity(verifier, "Diten.MDM", ServiceIdentityTokenAudiencePolicy.TrustedReferenceDataConsumer);

        var response = await Handler(identity, new GrantRepository(true), verifier).Handle(
            new IssueServiceIdentityTokenCommand(
                "mdm", "wrong", Guid.NewGuid(), ServiceIdentityTokenAudiencePolicy.TrustedReferenceDataConsumer),
            CancellationToken.None);

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Dedicated_reference_data_identity_without_enabled_grant_is_forbidden()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = Identity(verifier, "Diten.MDM", ServiceIdentityTokenAudiencePolicy.TrustedReferenceDataConsumer);

        var response = await Handler(identity, new GrantRepository(false), verifier).Handle(
            new IssueServiceIdentityTokenCommand(
                "mdm", "secret", Guid.NewGuid(), ServiceIdentityTokenAudiencePolicy.TrustedReferenceDataConsumer),
            CancellationToken.None);

        Assert.Equal(403, response.StatusCode);
    }

    [Theory]
    [InlineData("Other.Service", "TRUSTED_AUDIT_SOURCE_INGEST")]
    [InlineData("Other.Service", "TRUSTED_WORKFLOW_CONSUMER")]
    [InlineData("Other.Service", "TRUSTED_REFERENCE_DATA_CONSUMER")]
    [InlineData("Diten.MDM", "OTHER_AUDIENCE")]
    public async Task Wrong_service_or_audience_is_forbidden_even_if_grant_exists(string service, string audience)
    {
        var verifier = new ServiceClientCredentialVerifier();
        var response = await Handler(Identity(verifier, service), new GrantRepository(true), verifier).Handle(
            new IssueServiceIdentityTokenCommand("mdm", "secret", Guid.NewGuid(), audience), CancellationToken.None);
        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_client_performs_dummy_work_and_returns_401()
    {
        var verifier = new SpyVerifier();
        var response = await Handler(null, new GrantRepository(true), verifier).Handle(
            new IssueServiceIdentityTokenCommand("missing", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"),
            CancellationToken.None);
        Assert.Equal(401, response.StatusCode);
        Assert.Equal(1, verifier.UnknownCalls);
    }

    [Fact]
    public async Task Disabled_grant_is_forbidden()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var response = await Handler(Identity(verifier, "Diten.MDM"), new GrantRepository(false), verifier).Handle(
            new IssueServiceIdentityTokenCommand("mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"),
            CancellationToken.None);
        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task Missing_credential_version_is_state_conflict()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = Identity(verifier, "Diten.MDM");
        identity.ActiveCredentialVersion = string.Empty;
        var response = await Handler(identity, new GrantRepository(true), verifier).Handle(
            new IssueServiceIdentityTokenCommand("mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"),
            CancellationToken.None);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Partial_previous_state_is_409_only_after_valid_active_authentication()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var identity = Identity(verifier, "Diten.MDM");
        identity.PreviousCredentialHash = verifier.Hash("previous");
        var handler = Handler(identity, new GrantRepository(true), verifier);

        var authenticated = await handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);
        var unauthenticated = await handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "wrong", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);

        Assert.Equal(409, authenticated.StatusCode);
        Assert.Equal(401, unauthenticated.StatusCode);
    }

    [Fact]
    public async Task Wrong_credential_is_unauthorized()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var response = await Handler(Identity(verifier, "Diten.MDM"), new GrantRepository(true), verifier).Handle(
            new IssueServiceIdentityTokenCommand("mdm", "wrong", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"),
            CancellationToken.None);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Issuer_configuration_failure_maps_to_503()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var handler = Handler(Identity(verifier, "Diten.MDM"), new GrantRepository(true), verifier, new ThrowingIssuer());
        var response = await handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);
        Assert.Equal(503, response.StatusCode);
    }

    [Fact]
    public async Task Internal_two_second_budget_maps_to_504()
    {
        var handler = new IssueServiceIdentityTokenHandler(
            new BlockingIdentityRepository(), new GrantRepository(true), new SpyVerifier(), new StubIssuer(), new RecordingAudit(), TimeProvider.System);
        var response = await handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);
        Assert.Equal(504, response.StatusCode);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_from_handler()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new IssueServiceIdentityTokenHandler(
            new BlockingIdentityRepository(), new GrantRepository(true), new SpyVerifier(), new StubIssuer(), new RecordingAudit(), TimeProvider.System);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), cancellation.Token));
    }

    [Fact]
    public async Task Repository_unavailable_maps_narrowly_to_503()
    {
        var handler = new IssueServiceIdentityTokenHandler(
            new UnavailableIdentityRepository(), new GrantRepository(true), new SpyVerifier(), new StubIssuer(), new RecordingAudit(), TimeProvider.System);
        var response = await handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);
        Assert.Equal(503, response.StatusCode);
    }

    private static IssueServiceIdentityTokenHandler Handler(
        ServiceClientIdentity? identity,
        IServiceClientTenantGrantRepository grants,
        IServiceClientCredentialVerifier verifier,
        IServiceIdentityTokenIssuer? issuer = null,
        IAuthAuditService? audit = null,
        TimeProvider? clock = null) =>
        new(new IdentityRepository(identity), grants, verifier, issuer ?? new StubIssuer(), audit ?? new RecordingAudit(),
            clock ?? TimeProvider.System);

    private static ServiceClientIdentity Identity(
        ServiceClientCredentialVerifier verifier,
        string service,
        string? allowedAudience = null) => new()
    {
        ClientCode = "mdm",
        ServiceName = service,
        AllowedAudience = allowedAudience,
        ActiveCredentialHash = verifier.Hash("secret"),
        ActiveCredentialVersion = "v1"
    };

    private sealed class IdentityRepository(ServiceClientIdentity? value) : IServiceClientIdentityRepository
    {
        public Task<ServiceClientIdentity?> GetByClientCodeAsync(string clientCode, CancellationToken ct) => Task.FromResult(value);
    }

    private sealed class GrantRepository(bool granted) : IServiceClientTenantGrantRepository
    {
        public Guid TenantId { get; private set; }
        public Guid ClientId { get; private set; }
        public string? Audience { get; private set; }
        public Task<bool> HasEnabledGrantAsync(Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct)
        {
            TenantId = tenantId; ClientId = serviceClientIdentityId; Audience = audience;
            return Task.FromResult(granted);
        }
    }

    private sealed class StubIssuer : IServiceIdentityTokenIssuer
    {
        public ServiceIdentityTokenIssue Issue(Guid clientId, string serviceName, Guid tenantId, string audience) =>
            new("token", DateTimeOffset.UtcNow.AddMinutes(5), 300);
    }

    private sealed class ThrowingIssuer : IServiceIdentityTokenIssuer
    {
        public ServiceIdentityTokenIssue Issue(Guid clientId, string serviceName, Guid tenantId, string audience) =>
            throw new InvalidOperationException("invalid test configuration");
    }

    private sealed class BlockingIdentityRepository : IServiceClientIdentityRepository
    {
        public async Task<ServiceClientIdentity?> GetByClientCodeAsync(string clientCode, CancellationToken ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return null;
        }
    }

    private sealed class UnavailableIdentityRepository : IServiceClientIdentityRepository
    {
        public Task<ServiceClientIdentity?> GetByClientCodeAsync(string clientCode, CancellationToken ct) =>
            throw new ServiceIdentityPersistenceUnavailableException(new IOException("unavailable"));
    }

    private sealed class SpyVerifier : IServiceClientCredentialVerifier
    {
        public int UnknownCalls { get; private set; }
        public bool Verify(ServiceClientIdentity identity, string presentedSecret, DateTimeOffset nowUtc) => false;
        public bool IsStateCoherent(ServiceClientIdentity identity) => true;
        public void PerformUnknownClientWork(string presentedSecret) => UnknownCalls++;
        public string Hash(string secret) => string.Empty;
    }

    // WP-GP-AUDIT-TRAIL-01 D3 — the security event of a token request (ServiceIdentityTokenAudit).

    [Fact]
    public async Task An_issued_token_writes_one_security_row_with_the_key_and_no_secret_or_token()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var audit = new RecordingAudit();
        var tenantId = Guid.NewGuid();
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero));

        var response = await Handler(Identity(verifier, "Diten.MDM"), new GrantRepository(true), verifier, new DistinctIssuer(), audit, clock)
            .Handle(new IssueServiceIdentityTokenCommand("mdm", "secret", tenantId, "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var row = Assert.Single(audit.Rows.Values);
        // First: whatever else is wrong with the row, a secret or a token in it is the failure to report.
        AssertCarriesNoSecret(row, "secret", response.Data!.AccessToken, verifier.Hash("secret"));
        Assert.Equal(ServiceIdentityTokenAudit.IssuedEvent, row.EventName);
        Assert.Null(row.UserId);
        Assert.Equal(tenantId, row.TenantId);
        Assert.Equal(ServiceIdentityTokenAudit.IssuedAuditId("mdm", tenantId, "TRUSTED_AUDIT_SOURCE_INGEST", new DateOnly(2026, 10, 3)), row.Id);
        Assert.Contains("\"clientCode\":\"mdm\"", row.Metadata, StringComparison.Ordinal);
        Assert.Contains("\"dayUtc\":\"2026-10-03\"", row.Metadata, StringComparison.Ordinal);
        Assert.Contains("\"audience\":\"TRUSTED_AUDIT_SOURCE_INGEST\"", row.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_token_the_same_day_adds_no_row_but_the_next_day_and_another_tenant_do()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var audit = new RecordingAudit();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 3, 0, 0, 1, TimeSpan.Zero));
        var handler = Handler(Identity(verifier, "Diten.MDM"), new GrantRepository(true), verifier, audit: audit, clock: clock);
        Task<Response<ServiceIdentityTokenResponse>> Issue(Guid tenant) => handler.Handle(
            new IssueServiceIdentityTokenCommand("mdm", "secret", tenant, "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);

        Assert.True((await Issue(tenantA)).IsSuccessful);
        clock.Now = clock.Now.AddHours(23).AddMinutes(59);
        Assert.True((await Issue(tenantA)).IsSuccessful);
        Assert.Single(audit.Rows);
        Assert.Equal(2, audit.Attempts);

        Assert.True((await Issue(tenantB)).IsSuccessful);
        Assert.Equal(2, audit.Rows.Count);
        Assert.Contains(audit.Rows.Values, row => row.TenantId == tenantB);

        clock.Now = clock.Now.AddMinutes(1);
        Assert.True((await Issue(tenantA)).IsSuccessful);
        Assert.Equal(3, audit.Rows.Count);
    }

    [Fact]
    public async Task A_refusal_writes_a_coded_row_once_a_day_and_its_answer_is_unchanged()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var audit = new RecordingAudit();
        var tenantId = Guid.NewGuid();
        var handler = Handler(Identity(verifier, "Diten.MDM"), new GrantRepository(false), verifier, audit: audit);

        var first = await handler.Handle(new IssueServiceIdentityTokenCommand("mdm", "secret", tenantId, "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);
        var second = await handler.Handle(new IssueServiceIdentityTokenCommand("mdm", "secret", tenantId, "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);
        var wrong = await handler.Handle(new IssueServiceIdentityTokenCommand("mdm", "wrong-secret", tenantId, "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);

        Assert.Equal(403, first.StatusCode);
        Assert.Equal(403, second.StatusCode);
        Assert.Equal(401, wrong.StatusCode);
        Assert.Equal(2, audit.Rows.Count);
        Assert.All(audit.Rows.Values, row => Assert.Equal(ServiceIdentityTokenAudit.DeniedEvent, row.EventName));
        Assert.Contains(audit.Rows.Values, row => row.Metadata.Contains($"\"result\":\"{ServiceIdentityTokenAudit.Results.TenantGrantMissing}\"", StringComparison.Ordinal));
        Assert.Contains(audit.Rows.Values, row => row.Metadata.Contains($"\"result\":\"{ServiceIdentityTokenAudit.Results.InvalidCredential}\"", StringComparison.Ordinal));
        Assert.All(audit.Rows.Values, row => AssertCarriesNoSecret(row, "secret", "wrong-secret", verifier.Hash("secret")));
    }

    [Fact]
    public async Task An_unknown_client_is_recorded_as_such()
    {
        var audit = new RecordingAudit();
        var response = await Handler(null, new GrantRepository(true), new SpyVerifier(), audit: audit).Handle(
            new IssueServiceIdentityTokenCommand("missing", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);

        Assert.Equal(401, response.StatusCode);
        var row = Assert.Single(audit.Rows.Values);
        Assert.Contains($"\"result\":\"{ServiceIdentityTokenAudit.Results.UnknownClient}\"", row.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fifty_invented_client_codes_in_a_day_leave_one_row_and_none_of_the_codes()
    {
        var audit = new RecordingAudit();
        var tenantId = Guid.NewGuid();
        var handler = Handler(null, new GrantRepository(true), new SpyVerifier(), audit: audit);
        var codes = Enumerable.Range(0, 50).Select(_ => "invented-" + Guid.NewGuid().ToString("N")).ToList();

        foreach (var code in codes)
            Assert.Equal(401, (await handler.Handle(
                new IssueServiceIdentityTokenCommand(code, "secret", tenantId, "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None)).StatusCode);

        var row = Assert.Single(audit.Rows.Values);
        Assert.Equal(50, audit.Attempts);
        Assert.Contains($"\"clientCode\":\"{ServiceIdentityTokenAudit.UnknownClientCode}\"",
            System.Text.RegularExpressions.Regex.Unescape(row.Metadata), StringComparison.Ordinal);
        Assert.All(codes, code => Assert.DoesNotContain(code, row.Metadata, StringComparison.Ordinal));
    }

    [Fact]
    public async Task When_the_log_cannot_be_written_no_token_is_handed_out()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var response = await Handler(Identity(verifier, "Diten.MDM"), new GrantRepository(true), verifier, audit: new FailingAudit())
            .Handle(new IssueServiceIdentityTokenCommand("mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Null(response.Data);
        Assert.Equal(503, response.StatusCode);
        Assert.Contains(response.ErrorCodes, error => error.Code == IssueServiceIdentityTokenHandler.AuditUnavailableCode);
    }

    [Fact]
    public async Task When_the_log_cannot_be_written_a_refusal_keeps_its_answer()
    {
        var verifier = new ServiceClientCredentialVerifier();
        var response = await Handler(Identity(verifier, "Diten.MDM"), new GrantRepository(false), verifier, audit: new FailingAudit())
            .Handle(new IssueServiceIdentityTokenCommand("mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);

        Assert.Equal(403, response.StatusCode);
    }

    private static void AssertCarriesNoSecret(AuditRow row, params string[] secrets)
    {
        var all = string.Join("|", row.EventName, row.Metadata, row.TenantId, row.UserId, row.Id);
        foreach (var secret in secrets.Where(value => !string.IsNullOrEmpty(value)))
            Assert.DoesNotContain(secret, all, StringComparison.Ordinal);
    }

    private sealed record AuditRow(Guid Id, string EventName, Guid? UserId, Guid TenantId, string Metadata);

    /// <summary>The create-if-absent contract of <see cref="IAuthAuditService.WriteOnceAsync"/>, in memory; the real
    /// writer's honouring of it is proven against an owned mongod in the registry test project.</summary>
    private sealed class RecordingAudit : IAuthAuditService
    {
        public Dictionary<Guid, AuditRow> Rows { get; } = [];
        public int Attempts { get; private set; }
        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> WriteOnceAsync(Guid auditId, string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default)
        {
            Attempts++;
            return Task.FromResult(Rows.TryAdd(auditId, new AuditRow(auditId, eventName, userId, tenantId, metadata)));
        }
    }

    private sealed class FailingAudit : IAuthAuditService
    {
        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) => throw new IOException("log down");
        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default) => throw new IOException("log down");
        public Task<bool> WriteOnceAsync(Guid auditId, string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default) =>
            throw new IOException("log down");
    }

    /// <summary>A token no row could contain by accident ("token" itself is in the event name).</summary>
    private sealed class DistinctIssuer : IServiceIdentityTokenIssuer
    {
        public ServiceIdentityTokenIssue Issue(Guid clientId, string serviceName, Guid tenantId, string audience) =>
            new("eyJ" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddMinutes(5), 300);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

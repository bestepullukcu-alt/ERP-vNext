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

    [Theory]
    [InlineData("Other.Service", "TRUSTED_AUDIT_SOURCE_INGEST")]
    [InlineData("Other.Service", "TRUSTED_WORKFLOW_CONSUMER")]
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
            new BlockingIdentityRepository(), new GrantRepository(true), new SpyVerifier(), new StubIssuer(), TimeProvider.System);
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
            new BlockingIdentityRepository(), new GrantRepository(true), new SpyVerifier(), new StubIssuer(), TimeProvider.System);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), cancellation.Token));
    }

    [Fact]
    public async Task Repository_unavailable_maps_narrowly_to_503()
    {
        var handler = new IssueServiceIdentityTokenHandler(
            new UnavailableIdentityRepository(), new GrantRepository(true), new SpyVerifier(), new StubIssuer(), TimeProvider.System);
        var response = await handler.Handle(new IssueServiceIdentityTokenCommand(
            "mdm", "secret", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"), CancellationToken.None);
        Assert.Equal(503, response.StatusCode);
    }

    private static IssueServiceIdentityTokenHandler Handler(
        ServiceClientIdentity? identity,
        IServiceClientTenantGrantRepository grants,
        IServiceClientCredentialVerifier verifier,
        IServiceIdentityTokenIssuer? issuer = null) =>
        new(new IdentityRepository(identity), grants, verifier, issuer ?? new StubIssuer(), TimeProvider.System);

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
}

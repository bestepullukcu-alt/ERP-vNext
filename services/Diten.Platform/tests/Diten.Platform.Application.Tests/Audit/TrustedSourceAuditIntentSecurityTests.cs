using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Models.Audit;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class TrustedSourceAuditIntentSecurityTests
{
    private static readonly Guid TenantId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Fact]
    public void Credential_ActiveAndPreviousOverlapAreAcceptedWithExactTenantGrant()
    {
        var now = TrustedSourceAuditIntentTestData.Now;
        var authenticator = Authenticator(now, previousValidUntil: now.AddMinutes(1));

        var active = authenticator.Authenticate("mdm-source", "active-secret", "TRUSTED_AUDIT_SOURCE_INGEST");
        var previous = authenticator.Authenticate("mdm-source", "previous-secret", "TRUSTED_AUDIT_SOURCE_INGEST");

        Assert.True(active.IsAuthenticated);
        Assert.True(previous.IsAuthenticated);
        Assert.Contains(TenantId, active.AllowedTenantIds);
    }

    [Fact]
    public void Credential_ExpiredPreviousRevokedWrongAudienceAndWildcardFailClosed()
    {
        var now = TrustedSourceAuditIntentTestData.Now;

        Assert.False(Authenticator(now, previousValidUntil: now).Authenticate(
            "mdm-source", "previous-secret", "TRUSTED_AUDIT_SOURCE_INGEST").IsAuthenticated);
        Assert.False(Authenticator(now, revoked: true).Authenticate(
            "mdm-source", "active-secret", "TRUSTED_AUDIT_SOURCE_INGEST").IsAuthenticated);
        Assert.True(Authenticator(now).Authenticate("mdm-source", "active-secret", "wrong").IsForbidden);
        Assert.True(Authenticator(now, grants: ["*"]).Authenticate(
            "mdm-source", "active-secret", "TRUSTED_AUDIT_SOURCE_INGEST").IsForbidden);
        Assert.True(Authenticator(now, grants: []).Authenticate(
            "mdm-source", "active-secret", "TRUSTED_AUDIT_SOURCE_INGEST").IsForbidden);
    }

    [Theory]
    [InlineData("service", "Diten.MDM", "TRUSTED_AUDIT_SOURCE_INGEST", true)]
    [InlineData("user", "Diten.MDM", "TRUSTED_AUDIT_SOURCE_INGEST", false)]
    [InlineData("service", "Diten.Mdm", "TRUSTED_AUDIT_SOURCE_INGEST", false)]
    [InlineData("service", "Diten.MDM", "wrong", false)]
    public async Task Identity_RequiresExactServiceClaimsAndAudience(
        string actorType,
        string serviceName,
        string audience,
        bool expected)
    {
        var context = ContextWithPrincipal(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("actor_type", actorType),
            new Claim("service_name", serviceName),
            new Claim("tenant_id", TenantId.ToString("D")),
            new Claim("aud", audience)
        ], "test")));

        var result = await new TrustedSourceAuditIntentServiceIdentity().ResolveAsync(context, new HashSet<Guid> { TenantId });

        Assert.Equal(expected, result.IsAuthorized);
    }

    [Fact]
    public async Task Identity_RejectsDuplicateTenantClaimsAndTenantHeader()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("actor_type", "service"),
            new Claim("service_name", "Diten.MDM"),
            new Claim("tenant_id", TenantId.ToString("D")),
            new Claim("tenant_id", Guid.NewGuid().ToString("D")),
            new Claim("aud", "TRUSTED_AUDIT_SOURCE_INGEST")
        ], "test"));
        var duplicate = ContextWithPrincipal(principal);
        var header = ContextWithPrincipal(principal);
        header.Request.Headers["X-Tenant-Id"] = TenantId.ToString("D");
        var service = new TrustedSourceAuditIntentServiceIdentity();

        Assert.False((await service.ResolveAsync(duplicate, new HashSet<Guid> { TenantId })).IsAuthorized);
        Assert.False((await service.ResolveAsync(header, new HashSet<Guid> { TenantId })).IsAuthorized);
    }

    [Fact]
    public async Task Identity_RejectsClaimNameCaseDrift()
    {
        var context = ContextWithPrincipal(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("Actor_Type", "service"),
            new Claim("service_name", "Diten.MDM"),
            new Claim("tenant_id", TenantId.ToString("D")),
            new Claim("aud", "TRUSTED_AUDIT_SOURCE_INGEST")
        ], "test")));

        var result = await new TrustedSourceAuditIntentServiceIdentity().ResolveAsync(context, new HashSet<Guid> { TenantId });

        Assert.False(result.IsAuthorized);
    }

    [Fact]
    public async Task Executor_RejectsCredentialBeforeIdentityBodyAndRepositoryDispatch()
    {
        var credential = new Mock<ITrustedSourceAuditIntentCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(ITrustedSourceAuditIntentCredentialAuthenticator.AuthenticationResult.Unauthenticated);
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>(MockBehavior.Strict);
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var context = ValidRequestContext();
        var tracking = new TrackingStream(Encoding.UTF8.GetBytes(TrustedSourceAuditIntentTestData.Json()));
        context.Request.Body = tracking;
        var executor = new TrustedSourceAuditIntentRequestExecutor(
            credential.Object, identity.Object, new(), acceptance.Object, new TenantContext());

        var response = await Execute(executor, context);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.Equal(0, tracking.ReadCount);
        identity.VerifyNoOtherCalls();
        acceptance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executor_RejectsTenantMismatchBeforeAcceptanceAndRestoresContext()
    {
        var credential = new Mock<ITrustedSourceAuditIntentCredentialAuthenticator>();
        credential.Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new ITrustedSourceAuditIntentCredentialAuthenticator.AuthenticationResult(
                true, false, new HashSet<Guid> { TenantId }));
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>(), It.IsAny<IReadOnlySet<Guid>>()))
            .ReturnsAsync(new ITrustedSourceAuditIntentServiceIdentity.IdentityResult(true, true, Guid.NewGuid()));
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var tenantContext = new TenantContext();
        var executor = new TrustedSourceAuditIntentRequestExecutor(
            credential.Object, identity.Object, new(), acceptance.Object, tenantContext);

        var response = await Execute(executor, ValidRequestContext());

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.False(tenantContext.IsResolved);
        acceptance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executor_ValidRequestDispatchesInsideJwtTenantScopeAndRestoresIt()
    {
        var credential = new Mock<ITrustedSourceAuditIntentCredentialAuthenticator>();
        credential.Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new ITrustedSourceAuditIntentCredentialAuthenticator.AuthenticationResult(
                true, false, new HashSet<Guid> { TenantId }));
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>(), It.IsAny<IReadOnlySet<Guid>>()))
            .ReturnsAsync(new ITrustedSourceAuditIntentServiceIdentity.IdentityResult(true, true, TenantId));
        var tenantContext = new TenantContext();
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>();
        acceptance.Setup(x => x.AcceptAsync(It.IsAny<TrustedSourceAuditIntentEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns<TrustedSourceAuditIntentEnvelope, CancellationToken>((envelope, _) =>
            {
                Assert.True(tenantContext.IsResolved);
                Assert.Equal(TenantId, tenantContext.TenantId);
                return Task.FromResult(TrustedSourceAuditIntentAcceptanceResult.Accepted(new(
                    "ack", "key", envelope.ContractVersion, TrustedSourceAuditIntentTestData.Now, false)));
            });
        var executor = new TrustedSourceAuditIntentRequestExecutor(
            credential.Object, identity.Object, new(), acceptance.Object, tenantContext);

        var response = await Execute(executor, ValidRequestContext());

        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.False(tenantContext.IsResolved);
        acceptance.Verify(x => x.AcceptAsync(It.IsAny<TrustedSourceAuditIntentEnvelope>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executor_RejectsDuplicateHeadersAndOversizedBodyBeforeDispatch()
    {
        var executor = new TrustedSourceAuditIntentRequestExecutor(
            Mock.Of<ITrustedSourceAuditIntentCredentialAuthenticator>(),
            Mock.Of<ITrustedSourceAuditIntentServiceIdentity>(),
            new(),
            Mock.Of<ITrustedSourceAuditIntentAcceptanceService>(),
            new TenantContext());
        var duplicate = ValidRequestContext();
        duplicate.Request.Headers[TrustedSourceAuditIntentRequestExecutor.CredentialIdHeader] =
            new StringValues(new[] { "one", "two" });
        var oversized = ValidRequestContext();
        oversized.Request.ContentLength = TrustedSourceAuditIntentRequestExecutor.MaximumRequestBodyBytes + 1;

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(await Execute(executor, duplicate)).StatusCode);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<ObjectResult>(await Execute(executor, oversized)).StatusCode);
    }

    [Fact]
    public async Task Executor_RejectsMalformedBodyWith400()
    {
        var credential = new Mock<ITrustedSourceAuditIntentCredentialAuthenticator>();
        credential.Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new ITrustedSourceAuditIntentCredentialAuthenticator.AuthenticationResult(
                true, false, new HashSet<Guid> { TenantId }));
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>(), It.IsAny<IReadOnlySet<Guid>>()))
            .ReturnsAsync(new ITrustedSourceAuditIntentServiceIdentity.IdentityResult(true, true, TenantId));
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var context = ValidRequestContext();
        context.Request.Body = new MemoryStream("{}"u8.ToArray());
        context.Request.ContentLength = 2;
        var executor = new TrustedSourceAuditIntentRequestExecutor(
            credential.Object, identity.Object, new(), acceptance.Object, new TenantContext());

        var response = await Execute(executor, context);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(response).StatusCode);
        acceptance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executor_EnforcesTwoSecondLinkedBudgetWith504()
    {
        var credential = new Mock<ITrustedSourceAuditIntentCredentialAuthenticator>();
        credential.Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new ITrustedSourceAuditIntentCredentialAuthenticator.AuthenticationResult(
                true, false, new HashSet<Guid> { TenantId }));
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>(), It.IsAny<IReadOnlySet<Guid>>()))
            .ReturnsAsync(new ITrustedSourceAuditIntentServiceIdentity.IdentityResult(true, true, TenantId));
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>();
        acceptance.Setup(x => x.AcceptAsync(It.IsAny<TrustedSourceAuditIntentEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns<TrustedSourceAuditIntentEnvelope, CancellationToken>(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("unreachable");
            });
        var executor = new TrustedSourceAuditIntentRequestExecutor(
            credential.Object, identity.Object, new(), acceptance.Object, new TenantContext());

        var response = await Execute(executor, ValidRequestContext());

        Assert.Equal(StatusCodes.Status504GatewayTimeout, Assert.IsType<ObjectResult>(response).StatusCode);
    }

    public static IEnumerable<object[]> ControllerResults()
    {
        var receipt = new TrustedSourceAuditIntentAcceptanceReceipt("ack", "key", "mod-0290.audit-intent.v1", TrustedSourceAuditIntentTestData.Now, false);
        yield return [TrustedSourceAuditIntentAcceptanceResult.Accepted(receipt), 201];
        yield return [TrustedSourceAuditIntentAcceptanceResult.Duplicate(receipt), 200];
        yield return [TrustedSourceAuditIntentAcceptanceResult.Invalid(), 400];
        yield return [TrustedSourceAuditIntentAcceptanceResult.ContractUnsupported(), 409];
        yield return [TrustedSourceAuditIntentAcceptanceResult.MappingUnsupported(), 409];
        yield return [TrustedSourceAuditIntentAcceptanceResult.IdempotencyConflict(), 409];
        yield return [TrustedSourceAuditIntentAcceptanceResult.Unavailable(), 503];
    }

    [Theory]
    [MemberData(nameof(ControllerResults))]
    public async Task Controller_MapsControlledAcceptanceResultsToExactStatus(
        TrustedSourceAuditIntentAcceptanceResult acceptanceResult,
        int expectedStatus)
    {
        var executor = new ImmediateExecutor(acceptanceResult);
        var controller = new InternalTrustedSourceAuditIntentController(executor)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.Accept(CancellationToken.None);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(response).StatusCode);
    }

    private static TrustedSourceAuditIntentCredentialAuthenticator Authenticator(
        DateTimeOffset now,
        DateTimeOffset? previousValidUntil = null,
        bool revoked = false,
        List<string>? grants = null)
    {
        var options = new TrustedSourceAuditIntentCredentialOptions
        {
            Mdm = new()
            {
                Identifier = "mdm-source",
                ActiveSecret = "active-secret",
                PreviousSecret = "previous-secret",
                PreviousValidUntilUtc = previousValidUntil,
                IsRevoked = revoked,
                ConsumerService = "Diten.MDM",
                AllowedAudience = "TRUSTED_AUDIT_SOURCE_INGEST",
                AllowedTenantIds = grants ?? [TenantId.ToString("D")]
            }
        };
        return new(Options.Create(options), new FixedTimeProvider(now));
    }

    private static DefaultHttpContext ValidRequestContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer token";
        context.Request.Headers[TrustedSourceAuditIntentRequestExecutor.CredentialIdHeader] = "mdm-source";
        context.Request.Headers[TrustedSourceAuditIntentRequestExecutor.CredentialSecretHeader] = "active-secret";
        context.Request.Headers[TrustedSourceAuditIntentRequestExecutor.AudienceHeader] = "TRUSTED_AUDIT_SOURCE_INGEST";
        var bytes = Encoding.UTF8.GetBytes(TrustedSourceAuditIntentTestData.Json());
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        return context;
    }

    private static Task<IActionResult> Execute(TrustedSourceAuditIntentRequestExecutor executor, HttpContext context) =>
        executor.ExecuteAsync(
            context,
            CancellationToken.None,
            accepted => new ObjectResult(accepted) { StatusCode = accepted.IsAccepted ? 201 : 409 },
            (status, code) => new ObjectResult(code) { StatusCode = status });

    private static DefaultHttpContext ContextWithPrincipal(ClaimsPrincipal principal)
    {
        var authentication = new Mock<IAuthenticationService>();
        authentication.Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
            .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(principal, "Bearer")));
        var services = new ServiceCollection().AddSingleton(authentication.Object).BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int ReadCount { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class ImmediateExecutor(TrustedSourceAuditIntentAcceptanceResult acceptanceResult)
        : ITrustedSourceAuditIntentRequestExecutor
    {
        public Task<IActionResult> ExecuteAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken,
            Func<TrustedSourceAuditIntentAcceptanceResult, IActionResult> result,
            Func<int, string, IActionResult> failure) =>
            Task.FromResult(result(acceptanceResult));
    }
}

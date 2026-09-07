using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class TrustedSourceAuditIntentSecurityTests
{
    private static readonly Guid TenantId = Guid.Parse("20000000-0000-0000-0000-000000000002");

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
        var context = ContextWithAuthentication(AuthenticateResult.Success(new AuthenticationTicket(
            Principal(actorType, serviceName, audience),
            TrustedServiceTokenValidationExtensions.AuthenticationScheme)));

        var result = await new TrustedSourceAuditIntentServiceIdentity().ResolveAsync(context);

        Assert.Equal(expected, result.IsAuthorized);
        Assert.Equal(
            TrustedServiceTokenValidationExtensions.AuthenticationScheme,
            context.RequestServices.GetRequiredService<RecordingAuthenticationService>().LastScheme);
    }

    [Fact]
    public async Task Identity_RejectsDuplicateTenantClaimsAndTenantHeader()
    {
        var claims = Principal().Claims.Append(new Claim("tenant_id", Guid.NewGuid().ToString("D")));
        var duplicate = ContextWithAuthentication(Success(claims));
        var header = ContextWithAuthentication(Success(Principal().Claims));
        header.Request.Headers["X-Tenant-Id"] = TenantId.ToString("D");
        var service = new TrustedSourceAuditIntentServiceIdentity();

        Assert.False((await service.ResolveAsync(duplicate)).IsAuthorized);
        Assert.False((await service.ResolveAsync(header)).IsAuthorized);
    }

    [Fact]
    public async Task Identity_MapsNamedValidatorConfigurationFailureToUnavailable()
    {
        var context = ContextWithAuthentication(exception: new InvalidOperationException("invalid key configuration"));

        var result = await new TrustedSourceAuditIntentServiceIdentity().ResolveAsync(context);

        Assert.True(result.IsUnavailable);
        Assert.False(result.IsAuthenticated);
    }

    [Fact]
    public async Task Identity_DoesNotFallbackWhenNamedSchemeRejectsToken()
    {
        var context = ContextWithAuthentication(AuthenticateResult.Fail("invalid token"));

        var result = await new TrustedSourceAuditIntentServiceIdentity().ResolveAsync(context);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(
            TrustedServiceTokenValidationExtensions.AuthenticationScheme,
            context.RequestServices.GetRequiredService<RecordingAuthenticationService>().LastScheme);
    }

    [Theory]
    [InlineData("X-Audit-Source-Credential-Id")]
    [InlineData("X-Audit-Source-Credential")]
    [InlineData("X-Audit-Source-Audience")]
    [InlineData("X-Tenant-Id")]
    public async Task Executor_RejectsLegacyAuthorityHeadersBeforeIdentityBodyAndRepository(
        string forbiddenHeader)
    {
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>(MockBehavior.Strict);
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var context = ValidRequestContext();
        var tracking = new TrackingStream(Encoding.UTF8.GetBytes(TrustedSourceAuditIntentTestData.Json()));
        context.Request.Body = tracking;
        context.Request.Headers[forbiddenHeader] = "forbidden";
        var executor = Executor(identity.Object, acceptance.Object);

        var response = await Execute(executor, context);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.Equal(0, tracking.ReadCount);
        identity.VerifyNoOtherCalls();
        acceptance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executor_RejectsMissingAndDuplicateBearerBeforeIdentity()
    {
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>(MockBehavior.Strict);
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var missing = ValidRequestContext();
        missing.Request.Headers.Remove("Authorization");
        var duplicate = ValidRequestContext();
        duplicate.Request.Headers.Authorization = new StringValues(["Bearer one", "Bearer two"]);
        var executor = Executor(identity.Object, acceptance.Object);

        Assert.Equal(401, Assert.IsType<ObjectResult>(await Execute(executor, missing)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await Execute(executor, duplicate)).StatusCode);
        identity.VerifyNoOtherCalls();
        acceptance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executor_MapsUnavailableValidatorTo503BeforeBodyAndRepository()
    {
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(ITrustedSourceAuditIntentServiceIdentity.IdentityResult.Unavailable);
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var context = ValidRequestContext();
        var tracking = new TrackingStream(Encoding.UTF8.GetBytes(TrustedSourceAuditIntentTestData.Json()));
        context.Request.Body = tracking;
        var executor = Executor(identity.Object, acceptance.Object);

        var response = await Execute(executor, context);

        Assert.Equal(503, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.Equal(0, tracking.ReadCount);
        acceptance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executor_RejectsTokenEnvelopeTenantMismatchAndRestoresContext()
    {
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new ITrustedSourceAuditIntentServiceIdentity.IdentityResult(
                true, true, false, Guid.NewGuid()));
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var tenantContext = new TenantContext();
        var executor = Executor(identity.Object, acceptance.Object, tenantContext);

        var response = await Execute(executor, ValidRequestContext());

        Assert.Equal(403, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.False(tenantContext.IsResolved);
        acceptance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executor_ValidBearerDispatchesInsideTokenTenantScopeAndRestoresIt()
    {
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new ITrustedSourceAuditIntentServiceIdentity.IdentityResult(
                true, true, false, TenantId));
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
        var executor = Executor(identity.Object, acceptance.Object, tenantContext);

        var response = await Execute(executor, ValidRequestContext());

        Assert.Equal(201, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.False(tenantContext.IsResolved);
        acceptance.Verify(
            x => x.AcceptAsync(It.IsAny<TrustedSourceAuditIntentEnvelope>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Executor_OwnedTwoSecondBudgetMapsTo504AndRestoresTenantScope()
    {
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new ITrustedSourceAuditIntentServiceIdentity.IdentityResult(
                true, true, false, TenantId));
        var tenantContext = new TenantContext();
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>();
        acceptance.Setup(x => x.AcceptAsync(
                It.IsAny<TrustedSourceAuditIntentEnvelope>(),
                It.IsAny<CancellationToken>()))
            .Returns<TrustedSourceAuditIntentEnvelope, CancellationToken>((_, token) =>
                WaitForCancellation(token));
        var executor = Executor(identity.Object, acceptance.Object, tenantContext);

        var response = await Execute(executor, ValidRequestContext());

        Assert.Equal(504, Assert.IsType<ObjectResult>(response).StatusCode);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task Executor_CallerCancellationPropagatesWithoutTimeoutRemapping()
    {
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>(MockBehavior.Strict);
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var executor = Executor(identity.Object, acceptance.Object);
        using var caller = new CancellationTokenSource();
        caller.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Execute(executor, ValidRequestContext(), caller.Token));

        identity.VerifyNoOtherCalls();
        acceptance.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executor_RejectsOversizedAndMalformedBodyWithoutAcceptance()
    {
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new ITrustedSourceAuditIntentServiceIdentity.IdentityResult(
                true, true, false, TenantId));
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var executor = Executor(identity.Object, acceptance.Object);
        var oversized = ValidRequestContext();
        oversized.Request.ContentLength = TrustedSourceAuditIntentRequestExecutor.MaximumRequestBodyBytes + 1;
        var malformed = ValidRequestContext();
        malformed.Request.Body = new MemoryStream("{}"u8.ToArray());
        malformed.Request.ContentLength = 2;

        Assert.Equal(413, Assert.IsType<ObjectResult>(await Execute(executor, oversized)).StatusCode);
        Assert.Equal(400, Assert.IsType<ObjectResult>(await Execute(executor, malformed)).StatusCode);
        acceptance.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> ControllerResults()
    {
        var receipt = new TrustedSourceAuditIntentAcceptanceReceipt(
            "ack", "key", "mod-0290.audit-intent.v1", TrustedSourceAuditIntentTestData.Now, false);
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
        var controller = new InternalTrustedSourceAuditIntentController(new ImmediateExecutor(acceptanceResult))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.Accept(CancellationToken.None);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(response).StatusCode);
    }

    private static TrustedSourceAuditIntentRequestExecutor Executor(
        ITrustedSourceAuditIntentServiceIdentity identity,
        ITrustedSourceAuditIntentAcceptanceService acceptance,
        TenantContext? tenantContext = null) =>
        new(identity, new(), acceptance, tenantContext ?? new TenantContext());

    private static DefaultHttpContext ValidRequestContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer token";
        var bytes = Encoding.UTF8.GetBytes(TrustedSourceAuditIntentTestData.Json());
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        return context;
    }

    private static Task<IActionResult> Execute(
        TrustedSourceAuditIntentRequestExecutor executor,
        HttpContext context,
        CancellationToken cancellationToken = default) =>
        executor.ExecuteAsync(
            context,
            cancellationToken,
            accepted => new ObjectResult(accepted) { StatusCode = accepted.IsAccepted ? 201 : 409 },
            (status, code) => new ObjectResult(code) { StatusCode = status });

    private static async Task<TrustedSourceAuditIntentAcceptanceResult> WaitForCancellation(
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return TrustedSourceAuditIntentAcceptanceResult.Unavailable();
    }

    private static ClaimsPrincipal Principal(
        string actorType = "service",
        string serviceName = "Diten.MDM",
        string audience = "TRUSTED_AUDIT_SOURCE_INGEST") =>
        new(new ClaimsIdentity(
        [
            new Claim("actor_type", actorType),
            new Claim("service_name", serviceName),
            new Claim("tenant_id", TenantId.ToString("D")),
            new Claim("aud", audience)
        ], TrustedServiceTokenValidationExtensions.AuthenticationScheme));

    private static AuthenticateResult Success(IEnumerable<Claim> claims) =>
        AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                TrustedServiceTokenValidationExtensions.AuthenticationScheme)),
            TrustedServiceTokenValidationExtensions.AuthenticationScheme));

    private static DefaultHttpContext ContextWithAuthentication(
        AuthenticateResult? result = null,
        Exception? exception = null)
    {
        var authentication = new RecordingAuthenticationService(result, exception);
        var services = new ServiceCollection().AddSingleton(authentication).AddSingleton<IAuthenticationService>(
            authentication).BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private sealed class RecordingAuthenticationService(
        AuthenticateResult? result,
        Exception? exception) : IAuthenticationService
    {
        public string? LastScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            LastScheme = scheme;
            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(result ?? AuthenticateResult.NoResult());
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            throw new NotSupportedException();
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int ReadCount { get; private set; }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
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

using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Security;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class VerifiedReferenceDataRequestExecutorTests
{
    [Fact]
    public async Task Executor_UsesJwtTenantAndRestoresPriorScope()
    {
        var previous = Guid.NewGuid();
        var derived = Guid.NewGuid();
        var context = new TenantContext();
        context.SetTenant(previous);
        var credential = new Mock<IVerifiedGskuResolverCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(x => x.Authenticate("id", "secret", "VERIFIED_GSKU_RESOLVE"))
            .Returns(new VerifiedGskuResolverCredentialAuthenticationResult(true, false, "DITENMDMSERVICE", "VERIFIED_GSKU_RESOLVE"));
        var jwt = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        jwt.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new VerifiedGskuResolverJwtTenantResult(true, true, derived));
        var http = new DefaultHttpContext();
        http.Request.Headers[InternalBusinessReferenceDataController.CredentialIdHeader] = "id";
        http.Request.Headers[InternalBusinessReferenceDataController.CredentialSecretHeader] = "secret";
        http.Request.Headers[InternalBusinessReferenceDataController.AudienceHeader] = "VERIFIED_GSKU_RESOLVE";

        var result = await new VerifiedReferenceDataRequestExecutor(credential.Object, jwt.Object, context).ExecuteAsync(
            http, CancellationToken.None,
            (tenant, _) =>
            {
                Assert.Equal(derived, tenant);
                Assert.Equal(derived, context.TenantId);
                return Task.FromResult<IActionResult>(new OkResult());
            },
            (status, _) => new StatusCodeResult(status));

        Assert.IsType<OkResult>(result);
        Assert.Equal(previous, context.TenantId);
    }

    [Fact]
    public async Task Executor_RejectsCredentialBeforeJwtOrAction()
    {
        var credential = new Mock<IVerifiedGskuResolverCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(VerifiedGskuResolverCredentialAuthenticationResult.Unauthenticated);
        var jwt = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        var result = await new VerifiedReferenceDataRequestExecutor(credential.Object, jwt.Object, new TenantContext()).ExecuteAsync(
            new DefaultHttpContext(), CancellationToken.None,
            (_, _) => Task.FromResult<IActionResult>(new OkResult()),
            (status, _) => new StatusCodeResult(status));

        Assert.Equal(401, Assert.IsType<StatusCodeResult>(result).StatusCode);
        jwt.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executor_RestoresPriorScopeWhenActionThrows()
    {
        var previous = Guid.NewGuid();
        var context = new TenantContext();
        context.SetTenant(previous);
        var (executor, http, derived) = AuthorizedExecutor(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            http,
            CancellationToken.None,
            (_, _) =>
            {
                Assert.Equal(derived, context.TenantId);
                throw new InvalidOperationException("boom");
            },
            (status, _) => new StatusCodeResult(status)));

        Assert.Equal(previous, context.TenantId);
    }

    [Fact]
    public async Task Executor_RestoresPriorScopeWhenActionIsCancelled()
    {
        var previous = Guid.NewGuid();
        var context = new TenantContext();
        context.SetTenant(previous);
        var (executor, http, derived) = AuthorizedExecutor(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(
            http,
            cancellation.Token,
            (_, token) =>
            {
                Assert.Equal(derived, context.TenantId);
                return Task.FromCanceled<IActionResult>(token);
            },
            (status, _) => new StatusCodeResult(status)));

        Assert.Equal(previous, context.TenantId);
    }

    [Fact]
    public async Task Executor_MapsProviderTimeoutAndRestoresPriorScope()
    {
        var previous = Guid.NewGuid();
        var context = new TenantContext();
        context.SetTenant(previous);
        var (executor, http, derived) = AuthorizedExecutor(context);

        var result = await executor.ExecuteAsync(
            http,
            CancellationToken.None,
            (_, _) =>
            {
                Assert.Equal(derived, context.TenantId);
                throw new OperationCanceledException("provider timeout");
            },
            (status, _) => new StatusCodeResult(status));

        Assert.Equal(504, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Equal(previous, context.TenantId);
    }

    private static (VerifiedReferenceDataRequestExecutor Executor, DefaultHttpContext Http, Guid TenantId)
        AuthorizedExecutor(TenantContext context)
    {
        var tenantId = Guid.NewGuid();
        var credential = new Mock<IVerifiedGskuResolverCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(x => x.Authenticate("id", "secret", "VERIFIED_GSKU_RESOLVE"))
            .Returns(new VerifiedGskuResolverCredentialAuthenticationResult(
                true,
                false,
                "DITENMDMSERVICE",
                "VERIFIED_GSKU_RESOLVE"));
        var jwt = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        jwt.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new VerifiedGskuResolverJwtTenantResult(true, true, tenantId));
        var http = new DefaultHttpContext();
        http.Request.Headers[InternalBusinessReferenceDataController.CredentialIdHeader] = "id";
        http.Request.Headers[InternalBusinessReferenceDataController.CredentialSecretHeader] = "secret";
        http.Request.Headers[InternalBusinessReferenceDataController.AudienceHeader] = "VERIFIED_GSKU_RESOLVE";

        return (new VerifiedReferenceDataRequestExecutor(credential.Object, jwt.Object, context), http, tenantId);
    }
}

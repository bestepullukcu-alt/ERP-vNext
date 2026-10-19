using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Models.BusinessReferenceData;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedMarketAuthorizationTests
{
    [Theory]
    [InlineData(false, false, 401, "REFERENCE_UNAUTHENTICATED")]
    [InlineData(false, true, 403, "REFERENCE_FORBIDDEN")]
    public async Task InvalidCredentialIsRejectedBeforeJwtAndDispatch(
        bool authenticated,
        bool forbidden,
        int status,
        string reason)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var credential = new Mock<IVerifiedGskuResolverCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(value => value.Authenticate("id", "secret", "VERIFIED_GSKU_RESOLVE"))
            .Returns(new VerifiedGskuResolverCredentialAuthenticationResult(authenticated, forbidden, null, null));
        var jwt = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        var controller = Controller(mediator, credential, jwt, new TenantContext());

        var result = await controller.Resolve(
            new BusinessReferenceDataVerifiedMarketResolveRequest { MarketCode = "TR" },
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var response = Assert.IsType<Response<BusinessReferenceDataVerifiedMarketResolveResult>>(objectResult.Value);
        Assert.Equal(reason, response.ReasonCode);
        jwt.VerifyNoOtherCalls();
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueryOrUnknownRequestFieldIsRejectedBeforeDispatch()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = Controller(
            mediator,
            AuthenticatedCredential(),
            AuthorizedJwt(Guid.NewGuid()),
            new TenantContext());
        controller.Request.QueryString = new QueryString("?tenant_id=" + Guid.NewGuid());

        var result = await controller.Resolve(
            new BusinessReferenceDataVerifiedMarketResolveRequest { MarketCode = "TR" },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<Response<BusinessReferenceDataVerifiedMarketResolveResult>>(conflict.Value);
        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", response.ReasonCode);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizedEnumerationIsBodylessAndRestoresPreviousContext()
    {
        var previousTenantId = Guid.NewGuid();
        var jwtTenantId = Guid.NewGuid();
        var context = new TenantContext();
        context.SetTenant(previousTenantId);
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(value => value.Send(
                It.IsAny<EnumerateVerifiedMarketsQuery>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(jwtTenantId, context.TenantId))
            .ReturnsAsync(Response<BusinessReferenceDataVerifiedMarketsResult>.Success(
                new BusinessReferenceDataVerifiedMarketsResult([])));
        var controller = Controller(
            mediator,
            AuthenticatedCredential(),
            AuthorizedJwt(jwtTenantId),
            context);

        var result = await controller.EnumerateActive(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(previousTenantId, context.TenantId);
    }

    [Theory]
    [InlineData("resolve", 200)]
    [InlineData("enumerate", 403)]
    [InlineData("ambiguous", 403)]
    [InlineData("unauthorized-service", 403)]
    public async Task Background_service_resolve_is_tenant_bound_and_never_enumerates(string variant, int status)
    {
        var tenant = Guid.NewGuid();
        var previous = Guid.NewGuid();
        var context = new TenantContext();
        context.SetTenant(previous);
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        if (variant == "resolve")
            mediator.Setup(x => x.Send(It.IsAny<ResolveVerifiedMarketReferenceDataQuery>(), It.IsAny<CancellationToken>()))
                .Callback(() => Assert.Equal(tenant, context.TenantId))
                .ReturnsAsync(Response<BusinessReferenceDataVerifiedMarketResolveResult>.Success(new BusinessReferenceDataVerifiedMarketResolveResult(null!)));
        var interactive = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        interactive.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>())).ReturnsAsync(
            variant == "ambiguous" ? new(true, true, tenant) : VerifiedGskuResolverJwtTenantResult.Unauthenticated);
        var service = new Mock<IVerifiedReferenceDataServiceTenantContext>(MockBehavior.Strict);
        service.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>())).ReturnsAsync(
            new VerifiedGskuResolverJwtTenantResult(true, variant != "unauthorized-service", tenant));
        var controller = Controller(mediator, AuthenticatedCredential(), interactive, context, service.Object);
        var result = variant == "enumerate"
            ? await controller.EnumerateActive(default)
            : await controller.Resolve(new() { MarketCode = "TR" }, default);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Equal(previous, context.TenantId);
        if (variant != "resolve") mediator.VerifyNoOtherCalls();
    }

    private static InternalVerifiedMarketReferenceDataController Controller(
        Mock<IMediator> mediator,
        Mock<IVerifiedGskuResolverCredentialAuthenticator> credential,
        Mock<IVerifiedGskuResolverJwtTenantContext> jwt,
        TenantContext context,
        IVerifiedReferenceDataServiceTenantContext? service = null)
    {
        var controller = new InternalVerifiedMarketReferenceDataController(
            mediator.Object,
            credential.Object,
            jwt.Object,
            context,
            service);
        var http = new DefaultHttpContext();
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.CredentialIdHeader] = "id";
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.CredentialSecretHeader] = "secret";
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.AudienceHeader] = "VERIFIED_GSKU_RESOLVE";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static Mock<IVerifiedGskuResolverCredentialAuthenticator> AuthenticatedCredential()
    {
        var credential = new Mock<IVerifiedGskuResolverCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(value => value.Authenticate("id", "secret", "VERIFIED_GSKU_RESOLVE"))
            .Returns(new VerifiedGskuResolverCredentialAuthenticationResult(
                true,
                false,
                "DITENMDMSERVICE",
                "VERIFIED_GSKU_RESOLVE"));
        return credential;
    }

    private static Mock<IVerifiedGskuResolverJwtTenantContext> AuthorizedJwt(Guid tenantId)
    {
        var jwt = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        jwt.Setup(value => value.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new VerifiedGskuResolverJwtTenantResult(true, true, tenantId));
        return jwt;
    }
}

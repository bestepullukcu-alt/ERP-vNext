using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Models.BusinessReferenceData;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
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

        var result = await controller.Resolve(CancellationToken.None);

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

        var result = await controller.Resolve(CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<Response<BusinessReferenceDataVerifiedMarketResolveResult>>(conflict.Value);
        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", response.ReasonCode);
        mediator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{\"market_code\":\"TR\",\"market_code\":\"DE\"}")]
    [InlineData("{\"Market_Code\":\"TR\"}")]
    [InlineData("{\"market_code\":\"TR\",\"extra\":true}")]
    [InlineData("{\"market_code\":")]
    public async Task InvalidRawResolveContractMapsDeterministicallyTo409(string json)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = Controller(
            mediator,
            AuthenticatedCredential(),
            AuthorizedJwt(Guid.NewGuid()),
            new TenantContext());
        SetJsonBody(Assert.IsType<DefaultHttpContext>(controller.HttpContext), json);

        var result = await controller.Resolve(CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<Response<BusinessReferenceDataVerifiedMarketResolveResult>>(conflict.Value);
        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", response.ReasonCode);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OversizedRawResolveContractMapsDeterministicallyTo409()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = Controller(
            mediator,
            AuthenticatedCredential(),
            AuthorizedJwt(Guid.NewGuid()),
            new TenantContext());
        var json = "{\"market_code\":\"TR\",\"padding\":\""
            + new string('a', 2048)
            + "\"}";
        SetJsonBody(Assert.IsType<DefaultHttpContext>(controller.HttpContext), json);

        var result = await controller.Resolve(CancellationToken.None);

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
        controller.Request.Body = Stream.Null;
        controller.Request.ContentLength = null;

        var result = await controller.EnumerateActive(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(previousTenantId, context.TenantId);
    }

    [Fact]
    public async Task AuthorizedServiceIdentityResolvesMarketUnderTokenTenant()
    {
        var tenantId = Guid.NewGuid();
        var tenant = new TenantContext();
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(value => value.Send(
                It.Is<ResolveVerifiedMarketReferenceDataQuery>(query => query.MarketCode == "TR"),
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(tenantId, tenant.TenantId))
            .ReturnsAsync(Response<BusinessReferenceDataVerifiedMarketResolveResult>.Success(
                new BusinessReferenceDataVerifiedMarketResolveResult(
                    new BusinessReferenceDataVerifiedMarketSelection(
                        VerifiedMarketCatalogContract.SetCode,
                        "TR",
                        Guid.NewGuid(),
                        1,
                        VerifiedMarketCatalogContract.ResolutionMode,
                        DateTimeOffset.UtcNow))));
        var service = new Mock<IVerifiedReferenceDataServiceTenantContext>(MockBehavior.Strict);
        service.Setup(value => value.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new VerifiedGskuResolverJwtTenantResult(true, true, tenantId));
        var interactive = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        interactive.Setup(value => value.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(VerifiedGskuResolverJwtTenantResult.Unauthenticated);
        var controller = new InternalVerifiedMarketReferenceDataController(
            mediator.Object,
            AuthenticatedCredential().Object,
            interactive.Object,
            service.Object,
            tenant);
        var http = new DefaultHttpContext();
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.CredentialIdHeader] = "id";
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.CredentialSecretHeader] = "secret";
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.AudienceHeader] = "VERIFIED_GSKU_RESOLVE";
        SetJsonBody(http, "{\"market_code\":\"TR\"}");
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.Resolve(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Throws<InvalidOperationException>(() => tenant.TenantId);
    }

    [Fact]
    public async Task ServiceIdentityCannotEnumerateActiveMarkets()
    {
        var tenantId = Guid.NewGuid();
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var service = new Mock<IVerifiedReferenceDataServiceTenantContext>(MockBehavior.Strict);
        service.Setup(value => value.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new VerifiedGskuResolverJwtTenantResult(true, true, tenantId));
        var interactive = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        interactive.Setup(value => value.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(VerifiedGskuResolverJwtTenantResult.Unauthenticated);
        var controller = new InternalVerifiedMarketReferenceDataController(
            mediator.Object,
            AuthenticatedCredential().Object,
            interactive.Object,
            service.Object,
            new TenantContext());
        var http = new DefaultHttpContext();
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.CredentialIdHeader] = "id";
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.CredentialSecretHeader] = "secret";
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.AudienceHeader] = "VERIFIED_GSKU_RESOLVE";
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.EnumerateActive(CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<Response<BusinessReferenceDataVerifiedMarketsResult>>(forbidden.Value);
        Assert.Equal("REFERENCE_FORBIDDEN", response.ReasonCode);
        mediator.VerifyNoOtherCalls();
    }

    private static InternalVerifiedMarketReferenceDataController Controller(
        Mock<IMediator> mediator,
        Mock<IVerifiedGskuResolverCredentialAuthenticator> credential,
        Mock<IVerifiedGskuResolverJwtTenantContext> jwt,
        TenantContext context)
    {
        var controller = new InternalVerifiedMarketReferenceDataController(
            mediator.Object,
            credential.Object,
            jwt.Object,
            UnauthenticatedServiceContext().Object,
            context);
        var http = new DefaultHttpContext();
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.CredentialIdHeader] = "id";
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.CredentialSecretHeader] = "secret";
        http.Request.Headers[VerifiedReferenceDataRequestExecutor.AudienceHeader] = "VERIFIED_GSKU_RESOLVE";
        SetJsonBody(http, "{\"market_code\":\"TR\"}");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static Mock<IVerifiedReferenceDataServiceTenantContext> UnauthenticatedServiceContext()
    {
        var service = new Mock<IVerifiedReferenceDataServiceTenantContext>(MockBehavior.Strict);
        service.Setup(value => value.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(VerifiedGskuResolverJwtTenantResult.Unauthenticated);
        return service;
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

    private static void SetJsonBody(DefaultHttpContext http, string json)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        http.Request.Body = new MemoryStream(bytes);
        http.Request.ContentLength = bytes.Length;
        http.Request.ContentType = "application/json";
    }
}

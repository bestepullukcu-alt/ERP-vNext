using Diten.Platform.API.Controllers.Internal;
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

public sealed class BusinessReferenceDataVerifiedUomEnumerationAuthorizationTests
{
    [Theory]
    [InlineData(false, false, 401, "REFERENCE_UNAUTHENTICATED")]
    [InlineData(false, true, 403, "REFERENCE_FORBIDDEN")]
    public async Task MissingOrForbiddenServiceCredential_IsRejectedBeforeJwtAndDispatch(
        bool authenticated,
        bool forbidden,
        int expectedStatus,
        string expectedCode)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var credential = new Mock<IVerifiedGskuResolverCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(x => x.Authenticate("resolver-id", "resolver-secret", "VERIFIED_GSKU_RESOLVE"))
            .Returns(new VerifiedGskuResolverCredentialAuthenticationResult(
                authenticated, forbidden, null, null));
        var jwt = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        var controller = CreateController(mediator, credential, jwt, new TenantContext());

        var result = await controller.EnumerateUom(CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var response = Assert.IsType<Response<BusinessReferenceDataVerifiedUomResult>>(objectResult.Value);
        Assert.Equal(expectedCode, response.ReasonCode);
        jwt.VerifyNoOtherCalls();
        mediator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CallerControlledQueryOrBody_IsRejectedBeforeDispatch(bool queryInsteadOfBody)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var credential = AuthenticatedCredential();
        var jwt = AuthorizedJwt(Guid.NewGuid());
        var controller = CreateController(mediator, credential, jwt, new TenantContext());
        if (queryInsteadOfBody)
        {
            controller.Request.QueryString = new QueryString("?tenant_id=" + Guid.NewGuid());
        }
        else
        {
            controller.Request.ContentLength = 2;
        }

        var result = await controller.EnumerateUom(CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<Response<BusinessReferenceDataVerifiedUomResult>>(conflict.Value);
        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", response.ReasonCode);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizedBodylessRequest_UsesJwtTenantOnlyAndRestoresPriorContext()
    {
        var priorTenantId = Guid.NewGuid();
        var jwtTenantId = Guid.NewGuid();
        var context = new TenantContext();
        context.SetTenant(priorTenantId);
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(x => x.Send(
                It.IsAny<EnumerateVerifiedGskuUomsQuery>(), It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(jwtTenantId, context.TenantId))
            .ReturnsAsync(Response<BusinessReferenceDataVerifiedUomResult>.Success(
                new BusinessReferenceDataVerifiedUomResult([])));
        var controller = CreateController(
            mediator,
            AuthenticatedCredential(),
            AuthorizedJwt(jwtTenantId),
            context);

        var result = await controller.EnumerateUom(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(priorTenantId, context.TenantId);
        mediator.Verify(x => x.Send(
            It.IsAny<EnumerateVerifiedGskuUomsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static InternalBusinessReferenceDataController CreateController(
        Mock<IMediator> mediator,
        Mock<IVerifiedGskuResolverCredentialAuthenticator> credential,
        Mock<IVerifiedGskuResolverJwtTenantContext> jwt,
        TenantContext context)
    {
        var controller = new InternalBusinessReferenceDataController(
            mediator.Object, credential.Object, jwt.Object, context);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[InternalBusinessReferenceDataController.CredentialIdHeader] = "resolver-id";
        httpContext.Request.Headers[InternalBusinessReferenceDataController.CredentialSecretHeader] = "resolver-secret";
        httpContext.Request.Headers[InternalBusinessReferenceDataController.AudienceHeader] = "VERIFIED_GSKU_RESOLVE";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static Mock<IVerifiedGskuResolverCredentialAuthenticator> AuthenticatedCredential()
    {
        var credential = new Mock<IVerifiedGskuResolverCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(x => x.Authenticate("resolver-id", "resolver-secret", "VERIFIED_GSKU_RESOLVE"))
            .Returns(new VerifiedGskuResolverCredentialAuthenticationResult(
                true, false, "DITENMDMSERVICE", "VERIFIED_GSKU_RESOLVE"));
        return credential;
    }

    private static Mock<IVerifiedGskuResolverJwtTenantContext> AuthorizedJwt(Guid tenantId)
    {
        var jwt = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        jwt.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new VerifiedGskuResolverJwtTenantResult(true, true, tenantId));
        return jwt;
    }
}

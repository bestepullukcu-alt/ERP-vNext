using Diten.Platform.API.Configuration;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Models.BusinessReferenceData;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedResolverAuthorizationTests
{
    [Fact]
    public void ResolverCredential_IsServiceAndAudienceBound_AndCarriesNoTenant()
    {
        var authenticator = new VerifiedGskuResolverCredentialAuthenticator(
            Options.Create(new VerifiedGskuResolverCredentialOptions
            {
                Mdm = new VerifiedGskuResolverServiceCredentialOptions
                {
                    Identifier = "resolver-id",
                    ActiveSecret = "resolver-secret",
                    ConsumerService = "DITENMDMSERVICE",
                    AllowedAudience = "VERIFIED_GSKU_RESOLVE"
                }
            }),
            TimeProvider.System);

        var accepted = authenticator.Authenticate(
            "resolver-id", "resolver-secret", "VERIFIED_GSKU_RESOLVE");
        var wrongAudience = authenticator.Authenticate("resolver-id", "resolver-secret", "MODULE_REGISTRATION");
        var sharedCredential = authenticator.Authenticate("module-registration-id", "module-registration-secret", "VERIFIED_GSKU_RESOLVE");

        Assert.True(accepted.IsAuthenticated);
        Assert.Equal("DITENMDMSERVICE", accepted.ConsumerService);
        Assert.Equal("VERIFIED_GSKU_RESOLVE", accepted.AllowedAudience);
        Assert.True(wrongAudience.IsForbidden);
        Assert.False(sharedCredential.IsAuthenticated);
        Assert.Null(typeof(VerifiedGskuResolverServiceCredentialOptions).GetProperty("ConsumerTenantId"));
        Assert.Null(typeof(VerifiedGskuResolverCredentialAuthenticationResult).GetProperty("ConsumerTenantId"));
    }

    [Fact]
    public void WrongConfiguredService_IsForbidden()
    {
        var authenticator = new VerifiedGskuResolverCredentialAuthenticator(
            Options.Create(new VerifiedGskuResolverCredentialOptions
            {
                Mdm = new VerifiedGskuResolverServiceCredentialOptions
                {
                    Identifier = "resolver-id",
                    ActiveSecret = "resolver-secret",
                    ConsumerService = "OTHER_SERVICE",
                    AllowedAudience = "VERIFIED_GSKU_RESOLVE"
                }
            }),
            TimeProvider.System);

        var result = authenticator.Authenticate(
            "resolver-id", "resolver-secret", "VERIFIED_GSKU_RESOLVE");

        Assert.False(result.IsAuthenticated);
        Assert.True(result.IsForbidden);
    }

    [Fact]
    public void RevokedOrExpiredOverlapCredential_IsRejected()
    {
        var options = new VerifiedGskuResolverCredentialOptions
        {
            Mdm = new VerifiedGskuResolverServiceCredentialOptions
            {
                Identifier = "resolver-id",
                ActiveSecret = "active-secret",
                PreviousSecret = "expired-secret",
                PreviousValidUntilUtc = DateTimeOffset.Parse("2026-08-04T00:00:00Z"),
                ConsumerService = "DITENMDMSERVICE",
                AllowedAudience = "VERIFIED_GSKU_RESOLVE"
            }
        };
        var authenticator = new VerifiedGskuResolverCredentialAuthenticator(
            Options.Create(options),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-08-05T00:00:00Z")));

        Assert.False(authenticator.Authenticate(
            "resolver-id", "expired-secret", "VERIFIED_GSKU_RESOLVE").IsAuthenticated);
        options.Mdm.IsRevoked = true;
        Assert.False(authenticator.Authenticate(
            "resolver-id", "active-secret", "VERIFIED_GSKU_RESOLVE").IsAuthenticated);
    }

    [Theory]
    [InlineData(null, "resolver-secret")]
    [InlineData("resolver-id", null)]
    [InlineData("wrong-id", "resolver-secret")]
    [InlineData("resolver-id", "wrong-secret")]
    public void MissingOrWrongCredential_IsUnauthenticated(string? identifier, string? secret)
    {
        var authenticator = new VerifiedGskuResolverCredentialAuthenticator(
            Options.Create(new VerifiedGskuResolverCredentialOptions
            {
                Mdm = new VerifiedGskuResolverServiceCredentialOptions
                {
                    Identifier = "resolver-id",
                    ActiveSecret = "resolver-secret",
                    ConsumerService = "DITENMDMSERVICE",
                    AllowedAudience = "VERIFIED_GSKU_RESOLVE"
                }
            }),
            TimeProvider.System);

        var result = authenticator.Authenticate(identifier, secret, "VERIFIED_GSKU_RESOLVE");

        Assert.False(result.IsAuthenticated);
        Assert.False(result.IsForbidden);
    }

    [Fact]
    public async Task TenantQueryOverride_IsRejectedBeforeDispatch()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var credential = new Mock<IVerifiedGskuResolverCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(x => x.Authenticate("resolver-id", "resolver-secret", "VERIFIED_GSKU_RESOLVE"))
            .Returns(new VerifiedGskuResolverCredentialAuthenticationResult(
                true, false, "DITENMDMSERVICE", "VERIFIED_GSKU_RESOLVE"));
        var jwt = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        jwt.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(new VerifiedGskuResolverJwtTenantResult(true, true, Guid.NewGuid()));
        var controller = new InternalBusinessReferenceDataController(
            mediator.Object, credential.Object, jwt.Object, new TenantContext());
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[InternalBusinessReferenceDataController.CredentialIdHeader] = "resolver-id";
        httpContext.Request.Headers[InternalBusinessReferenceDataController.CredentialSecretHeader] = "resolver-secret";
        httpContext.Request.Headers[InternalBusinessReferenceDataController.AudienceHeader] = "VERIFIED_GSKU_RESOLVE";
        httpContext.Request.QueryString = new QueryString("?tenant_id=" + Guid.NewGuid());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Resolve(
            new BusinessReferenceDataVerifiedResolveRequest { Selections = [] },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<Response<BusinessReferenceDataVerifiedResolveResult>>(conflict.Value);
        Assert.Equal("REFERENCE_RESOLUTION_CONTRACT_INVALID", response.ReasonCode);
        mediator.VerifyNoOtherCalls();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.Application.Common;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Services.Mdm;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class CarrierAuthLegalEntitySuccessorTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegalEntityId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Correct_internal_key_binds_route_tenant_before_scope_resolution()
    {
        var tenantContext = new TenantContext();
        var internalContext = new InternalScopeResolutionContext();
        var resolver = new Mock<IDataScopeResolver>(MockBehavior.Strict);
        resolver.Setup(x => x.ResolveAsync(TenantId, ActorId, string.Empty, null, It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(TenantId, tenantContext.TenantId))
            .ReturnsAsync(new[] { new EntitlementDataScope(EntitlementDataScopeKind.LegalEntity, LegalEntityId, null) });
        var controller = CreateController("correct-key", "correct-key", resolver.Object, tenantContext, internalContext);

        var result = await controller.Resolve(TenantId, ActorId, CancellationToken.None);

        Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.True(internalContext.IsBound);
        Assert.Equal(TenantId, internalContext.TenantId);
        Assert.Equal(ActorId, internalContext.ActorId);
        resolver.VerifyAll();
    }

    [Fact]
    public async Task Wrong_internal_key_never_binds_tenant_or_reads_scope()
    {
        var tenantContext = new TenantContext();
        var internalContext = new InternalScopeResolutionContext();
        var resolver = new Mock<IDataScopeResolver>(MockBehavior.Strict);
        var controller = CreateController("correct-key", "wrong-key", resolver.Object, tenantContext, internalContext);

        var result = await controller.Resolve(TenantId, ActorId, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(401, objectResult.StatusCode);
        Assert.False(tenantContext.IsResolved);
        Assert.False(internalContext.IsBound);
        resolver.VerifyNoOtherCalls();
    }

    [Fact]
    public void Service_token_is_bound_to_exact_caller_scope_and_context()
    {
        var provider = new MdmServiceIdentityTokenProvider(Options.Create(ValidOptions()));

        var encoded = provider.Create(TenantId, ActorId, LegalEntityId);

        Assert.False(string.IsNullOrWhiteSpace(encoded));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded);
        Assert.Equal("platform-mdm-2026-09", token.Header.Kid);
        Assert.Equal("Diten.Platform", token.Subject);
        Assert.Equal(MdmServiceIdentityOptions.RequiredScope, token.Claims.Single(c => c.Type == "scope").Value);
        Assert.Equal(TenantId.ToString("D"), token.Claims.Single(c => c.Type == "tenant_id").Value);
        Assert.Equal(ActorId.ToString("D"), token.Claims.Single(c => c.Type == "actor_id").Value);
        Assert.Equal(LegalEntityId.ToString("D"), token.Claims.Single(c => c.Type == "legal_entity_id").Value);
    }

    [Theory]
    [InlineData(false, "01234567890123456789012345678901", 30)]
    [InlineData(true, "short", 30)]
    [InlineData(true, "01234567890123456789012345678901", 61)]
    public void Service_token_creation_fails_closed_on_disabled_or_invalid_configuration(
        bool enabled,
        string secret,
        int lifetime)
    {
        var options = ValidOptions();
        options.Enabled = enabled;
        options.Secret = secret;
        options.TokenLifetimeSeconds = lifetime;
        var provider = new MdmServiceIdentityTokenProvider(Options.Create(options));

        Assert.Null(provider.Create(TenantId, ActorId, LegalEntityId));
    }

    [Fact]
    public async Task Bound_internal_context_uses_only_the_narrow_MDM_service_route_and_identity()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId);
        var internalContext = new InternalScopeResolutionContext();
        internalContext.Bind(TenantId, ActorId);
        var tokenProvider = new MdmServiceIdentityTokenProvider(Options.Create(ValidOptions()));
        HttpRequestMessage? captured = null;
        var handler = new CaptureHandler(request =>
        {
            captured = request;
            var body = $$"""{"data":{"legalEntityId":"{{LegalEntityId:D}}","legalName":"LE","displayName":null,"lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });
        var validator = new MdmLegalEntityReferenceValidator(
            new HttpClient(handler),
            Options.Create(new MdmServiceOptions { BaseUrl = "http://mdm.test/" }),
            new HttpContextAccessor(),
            tenantContext,
            NullLogger<MdmLegalEntityReferenceValidator>.Instance,
            internalContext,
            tokenProvider);

        var result = await validator.ValidateAsync(LegalEntityId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(captured);
        Assert.Equal(
            $"http://mdm.test/api/internal/tenants/{TenantId:D}/legal-entities/{LegalEntityId:D}/reference-validation",
            captured!.RequestUri!.ToString());
        Assert.Equal(TenantId.ToString("D"), captured.Headers.GetValues("X-Tenant-Id").Single());
        Assert.Equal(ActorId.ToString("D"), captured.Headers.GetValues("X-Actor-Id").Single());
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task Missing_service_identity_configuration_fails_before_MDM_wire_call()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId);
        var internalContext = new InternalScopeResolutionContext();
        internalContext.Bind(TenantId, ActorId);
        var options = ValidOptions();
        options.Enabled = false;
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("wire call must not occur"));
        var validator = new MdmLegalEntityReferenceValidator(
            new HttpClient(handler),
            Options.Create(new MdmServiceOptions { BaseUrl = "http://mdm.test/" }),
            new HttpContextAccessor(),
            tenantContext,
            NullLogger<MdmLegalEntityReferenceValidator>.Instance,
            internalContext,
            new MdmServiceIdentityTokenProvider(Options.Create(options)));

        var result = await validator.ValidateAsync(LegalEntityId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, handler.CallCount);
    }

    private static InternalTenantLegalEntityScopeController CreateController(
        string expectedKey,
        string providedKey,
        IDataScopeResolver resolver,
        ITenantContext tenantContext,
        IInternalScopeResolutionContext internalContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AuthService:InternalApiKey"] = expectedKey })
            .Build();
        var controller = new InternalTenantLegalEntityScopeController(
            resolver,
            configuration,
            tenantContext,
            internalContext);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Headers["X-Internal-Api-Key"] = providedKey;
        return controller;
    }

    private static MdmServiceIdentityOptions ValidOptions() => new()
    {
        Enabled = true,
        Issuer = "diten-platform-service",
        Audience = "diten-mdm-reference-validation",
        CallerId = "Diten.Platform",
        KeyId = "platform-mdm-2026-09",
        Secret = "01234567890123456789012345678901",
        TokenLifetimeSeconds = 30
    };

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(response(request));
        }
    }
}

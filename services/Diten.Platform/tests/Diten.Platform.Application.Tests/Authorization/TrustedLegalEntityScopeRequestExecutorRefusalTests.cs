using System.Text;
using Diten.Platform.API.Security;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

/// <summary>
/// CT acceptance of the product-five takeover (2026-10-01). <c>InternalLegalEntityScopeResolutionController.Resolve</c>
/// is <c>[AllowAnonymous]</c>: the framework lets every request in, and <see cref="TrustedLegalEntityScopeRequestExecutor"/>
/// is the whole gate. Every existing executor test mocked that gate as "authenticated", so the refusals themselves were
/// unproven — measured by sabotage, removing <c>if (!credential.Authenticated)</c> turned nothing red.
///
/// <para>These tests are the proof the LoginOnly guard asks for before an anonymous internal controller is listed: each
/// refusal is exercised with the gate saying NO, and each asserts that nothing behind it ran — no later gate, no body
/// read, no action, no tenant scope.</para>
/// </summary>
public sealed class TrustedLegalEntityScopeRequestExecutorRefusalTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();

    [Theory]
    [InlineData(TrustedLegalEntityScopeRequestExecutor.CredentialIdHeader)]
    [InlineData(TrustedLegalEntityScopeRequestExecutor.CredentialSecretHeader)]
    [InlineData(TrustedLegalEntityScopeRequestExecutor.AudienceHeader)]
    public async Task A_missing_credential_header_is_401_before_any_gate_is_asked(string missing)
    {
        var order = new List<string>();
        var context = Context();
        context.Request.Headers.Remove(missing);

        var result = await Execute(Credential(order), Jwt(order), context);

        Assert.Equal((401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED"), result.Refusal);
        Assert.Empty(order);
        Assert.False(result.ActionRan);
    }

    [Theory]
    [InlineData(TrustedLegalEntityScopeRequestExecutor.CredentialIdHeader)]
    [InlineData(TrustedLegalEntityScopeRequestExecutor.CredentialSecretHeader)]
    [InlineData(TrustedLegalEntityScopeRequestExecutor.AudienceHeader)]
    public async Task A_duplicated_credential_header_is_401_before_any_gate_is_asked(string duplicated)
    {
        var order = new List<string>();
        var context = Context();
        context.Request.Headers[duplicated] = new[] { "a", "b" };

        var result = await Execute(Credential(order), Jwt(order), context);

        Assert.Equal((401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED"), result.Refusal);
        Assert.Empty(order);
        Assert.False(result.ActionRan);
    }

    [Fact]
    public async Task A_service_credential_that_does_not_authenticate_is_401_and_the_user_token_is_never_read()
    {
        var order = new List<string>();

        var result = await Execute(Credential(order, authenticated: false), Jwt(order), Context());

        Assert.Equal((401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED"), result.Refusal);
        Assert.Equal(new[] { "credential" }, order);
        Assert.False(result.ActionRan);
    }

    [Fact]
    public async Task A_service_credential_that_is_forbidden_is_403_and_the_user_token_is_never_read()
    {
        var order = new List<string>();

        var result = await Execute(Credential(order, authenticated: false, forbidden: true), Jwt(order), Context());

        Assert.Equal((403, "LEGAL_ENTITY_SCOPE_FORBIDDEN"), result.Refusal);
        Assert.Equal(new[] { "credential" }, order);
        Assert.False(result.ActionRan);
    }

    /// <summary>The service credential alone is not enough: a caller with no valid USER token resolves nothing.</summary>
    [Fact]
    public async Task A_valid_service_credential_without_an_authenticated_user_is_401()
    {
        var order = new List<string>();

        var result = await Execute(Credential(order), Jwt(order, authenticated: false, authorized: false), Context());

        Assert.Equal((401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED"), result.Refusal);
        Assert.Equal(new[] { "credential", "jwt" }, order);
        Assert.False(result.ActionRan);
    }

    public static TheoryData<bool, bool, bool> UserTokensThatAreNotEnough => new()
    {
        // authorized, has tenant, has subject
        { false, true, true },
        { true, false, true },
        { true, true, false }
    };

    [Theory]
    [MemberData(nameof(UserTokensThatAreNotEnough))]
    public async Task An_authenticated_user_that_is_not_authorized_or_carries_no_tenant_or_subject_is_403(
        bool authorized, bool hasTenant, bool hasSubject)
    {
        var order = new List<string>();
        var tenantContext = new TenantContext();

        var result = await Execute(
            Credential(order),
            Jwt(order, authorized: authorized, hasTenant: hasTenant, hasSubject: hasSubject),
            Context(),
            tenantContext);

        Assert.Equal((403, "LEGAL_ENTITY_SCOPE_FORBIDDEN"), result.Refusal);
        // Refused before the pair check and before any tenant scope was opened.
        Assert.Equal(new[] { "credential", "jwt" }, order);
        Assert.False(result.ActionRan);
        Assert.False(tenantContext.IsResolved);
    }

    private sealed record Outcome((int Status, string Code)? Refusal, bool ActionRan);

    private static async Task<Outcome> Execute(
        Mock<ITrustedLegalEntityScopeCredentialAuthenticator> credential,
        Mock<ITrustedLegalEntityScopeJwtContext> jwt,
        HttpContext context,
        TenantContext? tenantContext = null)
    {
        (int, string)? refusal = null;
        var actionRan = false;
        await new TrustedLegalEntityScopeRequestExecutor(credential.Object, jwt.Object, tenantContext ?? new TenantContext())
            .ExecuteAsync(
                context,
                default,
                (_, _, _, _) =>
                {
                    actionRan = true;
                    return Task.FromResult<IActionResult>(new OkResult());
                },
                (status, code) =>
                {
                    refusal = (status, code);
                    return new StatusCodeResult(status);
                });
        return new(refusal, actionRan);
    }

    private static Mock<ITrustedLegalEntityScopeCredentialAuthenticator> Credential(
        List<string> order, bool authenticated = true, bool forbidden = false)
    {
        var mock = new Mock<ITrustedLegalEntityScopeCredentialAuthenticator>();
        mock.Setup(x => x.Authenticate(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(() =>
            {
                order.Add("credential");
                return new(authenticated, forbidden);
            });
        mock.Setup(x => x.AllowsPair(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(() =>
            {
                order.Add("pair");
                return true;
            });
        return mock;
    }

    private static Mock<ITrustedLegalEntityScopeJwtContext> Jwt(
        List<string> order,
        bool authenticated = true,
        bool authorized = true,
        bool hasTenant = true,
        bool hasSubject = true)
    {
        var mock = new Mock<ITrustedLegalEntityScopeJwtContext>();
        var permissions = new HashSet<string>(StringComparer.Ordinal) { "mdm.gskus.read" };
        mock.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>()))
            .ReturnsAsync(() =>
            {
                order.Add("jwt");
                return new(authenticated, authorized, hasTenant ? Tenant : null, hasSubject ? Subject : null, permissions);
            });
        return mock;
    }

    private static DefaultHttpContext Context()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"module_code\":\"product-item-sku-master\",\"permission_key\":\"mdm.gskus.read\"}");
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        context.Request.Headers[TrustedLegalEntityScopeRequestExecutor.CredentialIdHeader] = "id";
        context.Request.Headers[TrustedLegalEntityScopeRequestExecutor.CredentialSecretHeader] = "secret";
        context.Request.Headers[TrustedLegalEntityScopeRequestExecutor.AudienceHeader] = "audience";
        return context;
    }
}

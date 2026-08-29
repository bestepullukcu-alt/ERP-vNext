using System.Text;
using Diten.Platform.API.Security;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

public sealed class TrustedLegalEntityScopeRequestExecutorTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();

    [Fact]
    public async Task Enforces_credential_then_JWT_then_parser_then_pair_permission_then_tenant_scope()
    {
        var order = new List<string>();
        var credential = Credential(order, allowed: true);
        var jwt = Jwt(order, ["mdm.gskus.read"]);
        var tenantContext = new TenantContext(); tenantContext.SetTenant(Guid.NewGuid()); var previous = tenantContext.TenantId;
        var executor = new TrustedLegalEntityScopeRequestExecutor(credential.Object, jwt.Object, tenantContext);

        var result = await executor.ExecuteAsync(Context(ValidJson()), default,
            (tenant, subject, request, _) =>
            {
                order.Add("action"); Assert.Equal(Tenant, tenantContext.TenantId);
                Assert.Equal(Tenant, tenant); Assert.Equal(Subject, subject); Assert.Equal("mdm.gskus.read", request.PermissionKey);
                return Task.FromResult<IActionResult>(new OkResult());
            }, Failure);

        Assert.IsType<OkResult>(result); Assert.Equal(new[] { "credential", "jwt", "pair", "action" }, order);
        Assert.Equal(previous, tenantContext.TenantId);
    }

    [Fact]
    public async Task Invalid_body_is_400_only_after_credential_and_JWT()
    {
        var order = new List<string>(); var executor = new TrustedLegalEntityScopeRequestExecutor(Credential(order).Object, Jwt(order, ["mdm.gskus.read"]).Object, new TenantContext());
        var result = await executor.ExecuteAsync(Context("{}"), default, (_, _, _, _) => throw new Xunit.Sdk.XunitException("action"), Failure);
        Assert.Equal(400, Assert.IsType<StatusCodeResult>(result).StatusCode); Assert.Equal(new[] { "credential", "jwt" }, order);
    }

    [Fact]
    public async Task Exact_pair_and_permission_mismatch_is_403()
    {
        var result = await new TrustedLegalEntityScopeRequestExecutor(Credential(new(), false).Object, Jwt(new(), ["mdm.gskus.read"]).Object, new TenantContext())
            .ExecuteAsync(Context(ValidJson()), default, (_, _, _, _) => throw new Xunit.Sdk.XunitException("action"), Failure);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(result).StatusCode);
        result = await new TrustedLegalEntityScopeRequestExecutor(Credential(new()).Object, Jwt(new(), ["mdm.lskus.read"]).Object, new TenantContext())
            .ExecuteAsync(Context(ValidJson()), default, (_, _, _, _) => throw new Xunit.Sdk.XunitException("action"), Failure);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task Caller_and_unrelated_dependency_cancellation_propagate_and_scope_restores()
    {
        var tenantContext = new TenantContext();
        var executor = new TrustedLegalEntityScopeRequestExecutor(Credential(new()).Object, Jwt(new(), ["mdm.gskus.read"]).Object, tenantContext);
        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsync(Context(ValidJson()), default,
            (_, _, _, _) => throw new OperationCanceledException("dependency"), Failure));
        Assert.False(tenantContext.IsResolved);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(Context(ValidJson()), cts.Token,
            (_, _, _, token) => Task.FromCanceled<IActionResult>(token), Failure));
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task Internal_two_second_budget_maps_only_its_own_timeout_to_504()
    {
        var executor = new TrustedLegalEntityScopeRequestExecutor(Credential(new()).Object, Jwt(new(), ["mdm.gskus.read"]).Object, new TenantContext());
        var result = await executor.ExecuteAsync(Context(ValidJson()), default,
            async (_, _, _, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new OkResult(); }, Failure);
        Assert.Equal(504, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task Duplicate_or_missing_credential_headers_are_401_before_JWT()
    {
        var order = new List<string>(); var context = Context(ValidJson()); context.Request.Headers[TrustedLegalEntityScopeRequestExecutor.CredentialIdHeader] = new[] { "a", "b" };
        var result = await new TrustedLegalEntityScopeRequestExecutor(Credential(order).Object, Jwt(order, ["mdm.gskus.read"]).Object, new TenantContext())
            .ExecuteAsync(context, default, (_, _, _, _) => throw new Xunit.Sdk.XunitException("action"), Failure);
        Assert.Equal(401, Assert.IsType<StatusCodeResult>(result).StatusCode); Assert.Empty(order);
    }

    private static Mock<ITrustedLegalEntityScopeCredentialAuthenticator> Credential(List<string> order, bool allowed = true)
    {
        var mock = new Mock<ITrustedLegalEntityScopeCredentialAuthenticator>();
        mock.Setup(x => x.Authenticate(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>())).Returns(() => { order.Add("credential"); return new(true, false); });
        mock.Setup(x => x.AllowsPair(It.IsAny<string>(), It.IsAny<string>())).Returns(() => { order.Add("pair"); return allowed; }); return mock;
    }
    private static Mock<ITrustedLegalEntityScopeJwtContext> Jwt(List<string> order, IEnumerable<string> permissions)
    {
        var mock = new Mock<ITrustedLegalEntityScopeJwtContext>();
        var exactPermissions = permissions.ToHashSet(StringComparer.Ordinal);
        mock.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>())).ReturnsAsync(() => { order.Add("jwt"); return new(true, true, Tenant, Subject, exactPermissions); }); return mock;
    }
    private static DefaultHttpContext Context(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json); var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json"; context.Request.ContentLength = bytes.Length; context.Request.Body = new MemoryStream(bytes);
        context.Request.Headers[TrustedLegalEntityScopeRequestExecutor.CredentialIdHeader] = "id";
        context.Request.Headers[TrustedLegalEntityScopeRequestExecutor.CredentialSecretHeader] = "secret";
        context.Request.Headers[TrustedLegalEntityScopeRequestExecutor.AudienceHeader] = "audience"; return context;
    }
    private static string ValidJson() => "{\"module_code\":\"product-item-sku-master\",\"permission_key\":\"mdm.gskus.read\"}";
    private static IActionResult Failure(int status, string _) => new StatusCodeResult(status);
}

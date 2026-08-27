using System.Text;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.Platform.Common.Tests.Authorization.Fu16;

public sealed class Fu16CanonicalRequestBindingTests
{
    private static readonly Guid Tenant = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Fact]
    public async Task Binding_is_deterministic_and_body_remains_readable()
    {
        var request = Request("POST", "/internal/gate-i", "{\"a\":1}");
        var first = await Fu16CanonicalRequestBinding.ComputeAsync(request, Tenant, "ppm.projects.read", ["ppm.projects.read"]);
        var second = await Fu16CanonicalRequestBinding.ComputeAsync(request, Tenant, "ppm.projects.read", ["ppm.projects.read"]);
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        Assert.Equal("{\"a\":1}", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Method_path_body_tenant_operation_and_permission_are_each_bound()
    {
        var baseline = await Hash("POST", "/x", "body", Tenant, "op", ["p"]);
        Assert.NotEqual(baseline, await Hash("PUT", "/x", "body", Tenant, "op", ["p"]));
        Assert.NotEqual(baseline, await Hash("POST", "/y", "body", Tenant, "op", ["p"]));
        Assert.NotEqual(baseline, await Hash("POST", "/x", "BODY", Tenant, "op", ["p"]));
        Assert.NotEqual(baseline, await Hash("POST", "/x", "body", Guid.NewGuid(), "op", ["p"]));
        Assert.NotEqual(baseline, await Hash("POST", "/x", "body", Tenant, "op2", ["p"]));
        Assert.NotEqual(baseline, await Hash("POST", "/x", "body", Tenant, "op", ["p2"]));
    }

    internal static HttpRequest Request(string method, string path, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context.Request;
    }

    private static Task<string> Hash(string method, string path, string body, Guid tenant, string operation, string[] permissions) =>
        Fu16CanonicalRequestBinding.ComputeAsync(Request(method, path, body), tenant, operation, permissions);
}

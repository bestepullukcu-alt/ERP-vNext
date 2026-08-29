using System.Text;
using Diten.Platform.Common.Authorization.S2S;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.Platform.Common.Tests.Authorization.S2S;

public sealed class S2SCanonicalRequestBindingTests
{
    private static readonly Guid Tenant = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Fact]
    public async Task ComputeAsync_WithCanonicalInput_IsDeterministicAndLeavesBodyReadable()
    {
        var request = Request("POST", "/internal/gate-i", "{\"a\":1}");

        var first = await S2SCanonicalRequestBinding.ComputeAsync(
            request,
            Tenant,
            "ppm.projects.read",
            ["ppm.projects.read"]);
        var second = await S2SCanonicalRequestBinding.ComputeAsync(
            request,
            Tenant,
            "ppm.projects.read",
            ["ppm.projects.read"]);

        Assert.Equal("cf9fcbbf4503eb207a0df5efa7504022d4cc0befa31082b2c3fc5da77317aaa1", first);
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        Assert.Equal("{\"a\":1}", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ComputeAsync_BindsEveryFieldAndPermissionOrder()
    {
        var baseline = await Hash("POST", "/x", "body", Tenant, "op", ["p1", "p2"]);

        Assert.NotEqual(baseline, await Hash("PUT", "/x", "body", Tenant, "op", ["p1", "p2"]));
        Assert.NotEqual(baseline, await Hash("POST", "/y", "body", Tenant, "op", ["p1", "p2"]));
        Assert.NotEqual(baseline, await Hash("POST", "/x", "BODY", Tenant, "op", ["p1", "p2"]));
        Assert.NotEqual(baseline, await Hash("POST", "/x", "body", Guid.NewGuid(), "op", ["p1", "p2"]));
        Assert.NotEqual(baseline, await Hash("POST", "/x", "body", Tenant, "op2", ["p1", "p2"]));
        Assert.NotEqual(baseline, await Hash("POST", "/x", "body", Tenant, "op", ["p2", "p1"]));
    }

    [Fact]
    public async Task ComputeAsync_WithInvalidAuthorityInputs_FailsClosed()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            S2SCanonicalRequestBinding.ComputeAsync(Request("POST", "/x", "body"), Guid.Empty, "op", ["p"]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            S2SCanonicalRequestBinding.ComputeAsync(Request("POST", "/x", "body"), Tenant, " ", ["p"]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            S2SCanonicalRequestBinding.ComputeAsync(Request("POST", "/x", "body"), Tenant, "op", []));
    }

    private static HttpRequest Request(string method, string path, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context.Request;
    }

    private static Task<string> Hash(
        string method,
        string path,
        string body,
        Guid tenant,
        string operation,
        string[] permissions) =>
        S2SCanonicalRequestBinding.ComputeAsync(Request(method, path, body), tenant, operation, permissions);
}

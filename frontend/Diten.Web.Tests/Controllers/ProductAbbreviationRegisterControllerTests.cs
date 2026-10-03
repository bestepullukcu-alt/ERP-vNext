using System.Net;
using System.Security.Claims;
using Diten.Web.Controllers;
using Diten.Web.Models.ProductAbbreviationRegister;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

public sealed class ProductAbbreviationRegisterControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Requester_needs_exact_GlobalProductRead_for_selector_and_request(bool productRead)
    {
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.product-abbreviations.read", "mdm.product-abbreviations.request",
            productRead ? "mdm.global-products.read" : "unrelated");
        Assert.IsType<ViewResult>(controller.Index());
        var selector = await controller.GlobalProductSelector(default);
        var request = await controller.RequestAllocation(new() { GlobalProductId = Guid.NewGuid(), Abbreviation = "ABC" }, default);
        if (productRead) { Assert.IsType<ContentResult>(selector); Assert.IsType<ContentResult>(request); Assert.Equal(2, wire.Paths.Count); }
        else { AssertDenied(selector); AssertDenied(request); Assert.Empty(wire.Paths); }
    }

    [Fact]
    public async Task Auditor_reads_register_and_evidence_without_selector_permission_or_call()
    {
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.product-abbreviations.read", "mdm.product-abbreviations.audit");
        Assert.IsType<ViewResult>(controller.Index());
        Assert.IsType<ContentResult>(await controller.GetByGlobalProduct(Guid.NewGuid(), default));
        Assert.IsType<ContentResult>(await controller.GetEvidence(Guid.NewGuid(), default));
        AssertDenied(await controller.GlobalProductSelector(default));
        AssertDenied(await controller.RequestAllocation(new(), default));
        Assert.Equal(2, wire.Paths.Count);
        Assert.DoesNotContain(wire.Paths, path => path.Contains("selector"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("mdm.global-products.Read")]
    [InlineData("mdm.product-abbreviations.request")]
    public async Task Nonexact_selector_permission_does_not_proxy(string permission)
    {
        using var wire = new Wire();
        AssertDenied(await Controller(wire, permission).GlobalProductSelector(default));
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task Mutation_replay_identity_is_server_owned_tenant_subject_target_and_payload_bound()
    {
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.product-abbreviations.retire");
        var id = Guid.NewGuid();
        Assert.IsType<ContentResult>(await controller.RequestRetirement(id, new(4, "fixture reason"), default));
        await controller.RequestRetirement(id, new(4, "fixture reason"), default);
        Assert.Equal(wire.Keys[0], wire.Keys[1]);
        await controller.RequestRetirement(id, new(5, "fixture reason"), default);
        await controller.RequestRetirement(Guid.NewGuid(), new(4, "fixture reason"), default);
        Assert.NotEqual(wire.Keys[0], wire.Keys[2]);
        Assert.NotEqual(wire.Keys[0], wire.Keys[3]);
        var identity = (ClaimsIdentity)controller.User.Identity!;
        identity.RemoveClaim(identity.FindFirst("sub")!);
        identity.AddClaim(new("sub", Guid.NewGuid().ToString("D")));
        await controller.RequestRetirement(id, new(4, "fixture reason"), default);
        Assert.NotEqual(wire.Keys[0], wire.Keys[4]);
        identity.RemoveClaim(identity.FindFirst("tenant_id")!);
        identity.AddClaim(new("tenant_id", Guid.NewGuid().ToString("D")));
        await controller.RequestRetirement(id, new(4, "fixture reason"), default);
        Assert.NotEqual(wire.Keys[0], wire.Keys[5]);
    }

    [Theory]
    [InlineData(202)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task Backend_failure_status_and_envelope_are_not_reported_as_success(int status)
    {
        using var wire = new Wire { Status = status };
        var controller = Controller(wire, "mdm.product-abbreviations.retire");
        var result = Assert.IsType<ContentResult>(await controller.RequestRetirement(Guid.NewGuid(), new(4, "reason"), default));
        Assert.Equal(status, result.StatusCode);
        Assert.Contains("\"isSuccessful\":false", result.Content);
    }

    [Fact]
    public async Task Duplicate_tenant_and_conflicting_subject_fail_before_transport()
    {
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.product-abbreviations.retire");
        var identity = (ClaimsIdentity)controller.User.Identity!;
        identity.AddClaim(new("tenant_id", identity.FindFirst("tenant_id")!.Value));
        Assert.IsType<UnauthorizedObjectResult>(await controller.RequestRetirement(Guid.NewGuid(), new(4, "reason"), default));
        identity.RemoveClaim(identity.FindAll("tenant_id").Last());
        identity.AddClaim(new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")));
        Assert.IsType<UnauthorizedResult>(await controller.RequestRetirement(Guid.NewGuid(), new(4, "reason"), default));
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public void A_reader_without_permission_gets_a_403_page_inside_the_shell_not_a_redirect()
    {
        // FIX1 S5 (UAS-001) — Forbid() answered 302 to an AccessDeniedPath this app does not have (→ 404).
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.global-products.read");

        var result = controller.Index();

        Assert.Equal("~/Views/MDM/ProductAbbreviationRegister/Index.cshtml", Assert.IsType<ViewResult>(result).ViewName);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task Every_proxy_refuses_a_missing_permission_with_a_coded_403_and_never_calls_the_gateway()
    {
        using var wire = new Wire();
        var controller = Controller(wire, "unrelated");
        var id = Guid.NewGuid();

        AssertDenied(await controller.GetByGlobalProduct(id, default));
        AssertDenied(await controller.ListByGlobalProduct(id, default));
        AssertDenied(await controller.GetEvidence(id, default));
        AssertDenied(await controller.Cancel(id, new(0), default));
        AssertDenied(await controller.Approve(id, new(0), default));
        AssertDenied(await controller.Reject(id, new(0, "r"), default));
        AssertDenied(await controller.InitiateCorrection(id, new(0, "ABC", "r"), default));
        AssertDenied(await controller.RequestRetirement(id, new(0, "r"), default));
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task The_page_list_reads_every_entry_of_the_product_through_the_gateway()
    {
        using var wire = new Wire();
        var id = Guid.NewGuid();

        Assert.IsType<ContentResult>(await Controller(wire, "mdm.product-abbreviations.read").ListByGlobalProduct(id, default));

        Assert.Equal($"/api/product-abbreviations/by-global-product/{id:D}/entries", Assert.Single(wire.Paths));
    }

    private static void AssertDenied(IActionResult result)
    {
        var denied = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);
        Assert.Contains("ABBREVIATION_PERMISSION_DENIED", System.Text.Json.JsonSerializer.Serialize(denied.Value));
    }

    private static ProductAbbreviationRegisterController Controller(Wire wire, params string[] permissions)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString("D")), new("tenant_id", Guid.NewGuid().ToString("D")) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var context = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "fixture")) };
        // Synthetic forwarding fixture only; no real JWT or credential and no live HTTP destination.
        context.Request.Headers.Cookie = "access_token=synthetic-abb-fixture";
        return new(new HttpClient(wire), new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?> { ["GatewayUrl"] = "https://gateway-fixture.invalid" }).Build(),
            new Localizer(), NullLogger<ProductAbbreviationRegisterController>.Instance)
        { ControllerContext = new() { HttpContext = context } };
    }

    private sealed class Wire : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string> Keys { get; } = [];
        public int Status { get; init; } = 200;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Paths.Add(request.RequestUri!.AbsolutePath);
            if (request.Headers.TryGetValues("Idempotency-Key", out var keys)) Keys.Add(Assert.Single(keys));
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Single(request.Headers.GetValues("X-Tenant-Id"));
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)Status) {
                Content = new StringContent(Status == 200 ? "{\"isSuccessful\":true,\"data\":{}}" : "{\"isSuccessful\":false,\"errors\":[\"fixture-failure\"]}")
            });
        }
    }
    private sealed class Localizer : IStringLocalizer<Diten.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

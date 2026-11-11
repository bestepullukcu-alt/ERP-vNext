using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.PlanningService.Api.Features.Mrp;
using Diten.PlanningService.Application.Features.Mrp;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class MrpLeadTimeAuthorityTests
{
    [Fact]
    public async Task ActiveCompanyAndPermission_Allow_WhileWrongTenantAndMissingPermissionDeny()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var context = HttpContext(tenant, actor, "mrp.lead-times.create");
        var transport = new Transport(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json($"{{\"data\":{{\"legalEntityId\":\"{company:D}\",\"referenceable\":true}}}}")
        });
        var reader = new MdmLegalEntityScopeReader(new Factory(transport), new HttpContextAccessor { HttpContext = context });
        var authority = new MdmLeadTimeAuthority(new HttpContextAccessor { HttpContext = context }, reader);
        var command = Command(tenant, company, actor);

        Assert.Equal(LeadTimeAuthorityResult.Allowed,
            await authority.CanExecuteAsync(command, "mrp.lead-times.create", default));
        Assert.Equal(LeadTimeAuthorityResult.Forbidden,
            await authority.CanExecuteAsync(command, "mrp.lead-times.review", default));
        Assert.Equal(LeadTimeAuthorityResult.Forbidden,
            await authority.CanExecuteAsync(command with { TenantId = Guid.NewGuid() },
                "mrp.lead-times.create", default));
        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, LeadTimeAuthorityResult.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable, LeadTimeAuthorityResult.Unavailable)]
    public async Task MdmReferenceOutcome_FailsClosed(HttpStatusCode status, LeadTimeAuthorityResult expected)
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var context = HttpContext(tenant, actor, "mrp.lead-times.create");
        var transport = new Transport(_ => new HttpResponseMessage(status));
        var reader = new MdmLegalEntityScopeReader(new Factory(transport), new HttpContextAccessor { HttpContext = context });
        var authority = new MdmLeadTimeAuthority(new HttpContextAccessor { HttpContext = context }, reader);
        Assert.Equal(expected, await authority.CanExecuteAsync(
            Command(tenant, Guid.NewGuid(), actor), "mrp.lead-times.create", default));
    }

    [Fact]
    public async Task CompanyList_UsesSameReaderAndOnlyReferenceableRows()
    {
        var tenant = Guid.NewGuid();
        var active = Guid.NewGuid();
        var inactive = Guid.NewGuid();
        var context = HttpContext(tenant, Guid.NewGuid(), "mrp.lead-times.create");
        var transport = new Transport(request =>
        {
            Assert.Equal("/api/legal-entities/lookup", request.RequestUri!.AbsolutePath);
            Assert.Equal(tenant.ToString("D"), request.Headers.GetValues("X-Tenant-Id").Single());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json($"{{\"data\":[{{\"legalEntityId\":\"{active:D}\",\"referenceable\":true}},{{\"legalEntityId\":\"{inactive:D}\",\"referenceable\":false}}]}}")
            };
        });
        var reader = new MdmLegalEntityScopeReader(new Factory(transport), new HttpContextAccessor { HttpContext = context });
        Assert.Equal([active], await reader.ListActiveAsync(tenant, default));
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    private static DefaultHttpContext HttpContext(Guid tenant, Guid actor, string permission)
    {
        var context = new DefaultHttpContext();
        context.Items["DemandPlanning.TenantId"] = tenant;
        context.Request.Headers.Authorization = "Bearer fixture-token";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", actor.ToString("D")), new Claim("permission", permission)
        ], "fixture"));
        return context;
    }

    private static LeadTimeCommand Command(Guid tenant, Guid company, Guid actor) =>
        new(LeadTimeOperation.Create, tenant, company, Guid.NewGuid(), Guid.NewGuid(), actor,
            Guid.Empty, Guid.Empty, 7, new DateOnly(2026, 10, 19), null, null, null, "fixture-key");

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false)
        {
            BaseAddress = new Uri("http://127.0.0.1:5000/")
        };
    }

    private sealed class Transport(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }
}

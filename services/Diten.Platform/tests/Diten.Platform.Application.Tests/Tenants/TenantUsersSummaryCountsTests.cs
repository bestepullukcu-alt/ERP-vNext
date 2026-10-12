using System.Net;
using System.Text;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

/// <summary>
/// BL-459 — the platform tenant users summary counts the tenant's users where they live (AuthService), not only
/// Platform's own invited-administrator list; AuthService unreachable → the old count, so the page still renders.
/// </summary>
public sealed class TenantUsersSummaryCountsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task The_summary_is_AuthServices_count_of_the_tenants_users()
    {
        var reader = new Mock<ITenantUserCountReader>();
        reader.Setup(r => r.GetCountsAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(new TenantUserCounts(12, 9, 2, 1));

        var summary = await Handler(reader.Object).Handle(new GetTenantUsersSummaryQuery(TenantId), CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(12, summary!.TotalUsers);
        Assert.Equal(9, summary.ActiveUsers);
        Assert.Equal(2, summary.PendingInvitations);
    }

    [Fact]
    public async Task Without_AuthService_the_summary_falls_back_to_the_admin_list()
    {
        var reader = new Mock<ITenantUserCountReader>();
        reader.Setup(r => r.GetCountsAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((TenantUserCounts?)null);

        var summary = await Handler(reader.Object).Handle(new GetTenantUsersSummaryQuery(TenantId), CancellationToken.None);

        Assert.Equal(1, summary!.TotalUsers); // the one invited administrator Platform knows about
    }

    [Fact]
    public async Task The_client_asks_for_the_tenant_and_refuses_an_answer_about_another()
    {
        var foreign = Guid.NewGuid();
        var handler = new RecordingHandler(TenantId,
            $$"""{"tenantId":"{{foreign}}","total":99,"active":99,"invited":0,"inactive":0}""");
        var client = Client(handler);

        Assert.Null(await client.GetCountsAsync(TenantId));
        Assert.Equal($"/internal/users/counts?tenantId={TenantId:D}", handler.LastPathAndQuery);
        Assert.True(handler.HadInternalKey);
    }

    [Fact]
    public async Task The_client_reads_the_tenants_own_counts()
    {
        var handler = new RecordingHandler(TenantId,
            $$"""{"tenantId":"{{TenantId}}","total":6,"active":3,"invited":2,"inactive":1}""");

        Assert.Equal(new TenantUserCounts(6, 3, 2, 1), await Client(handler).GetCountsAsync(TenantId));
    }

    private static GetTenantUsersSummaryQueryHandler Handler(ITenantUserCountReader reader)
    {
        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(new Tenant
        {
            Id = TenantId,
            Code = "TEN123",
            Slug = "acme",
            Name = "Acme",
            DisplayName = "Acme",
            Domain = "acme.ditenteknoloji.com",
            AdminUsers = [new TenantAdminUser { Name = "a@acme.test", Email = "a@acme.test", Status = TenantAdminUserStatus.Invited }]
        });
        return new GetTenantUsersSummaryQueryHandler(repository.Object, reader);
    }

    private static AuthTenantUserCountClient Client(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return new AuthTenantUserCountClient(
            factory.Object,
            Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.test", InternalApiKey = "k" }),
            NullLogger<AuthTenantUserCountClient>.Instance);
    }

    private sealed class RecordingHandler(Guid tenant, string body) : HttpMessageHandler
    {
        public string? LastPathAndQuery { get; private set; }
        public bool HadInternalKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = tenant;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            HadInternalKey = request.Headers.Contains("X-Internal-Api-Key");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}

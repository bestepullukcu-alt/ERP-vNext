using Diten.PpmService.Application.Features.Portfolios;
using Xunit;

namespace Diten.PpmService.IntegrationTests.Portfolios;

public sealed class PortfolioAuthTransportIntegrationTests
{
    [Theory]
    [InlineData("untrusted-root")]
    [InlineData("hostname")]
    [InlineData("expired")]
    public async Task Invalid_tls_rejects_connection_before_any_http_credential(string mode)
    {
        var server = new PortfolioAuthTransportTestHost(certificateMode: mode);
        try
        {
            var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
            using var client = server.Client(trustRoot: mode != "untrusted-root");
            var result = await server.Authority(client, tenant, actor, "run-owned-test-credential").CandidatesAsync(Scope(tenant, actor), default);
            Assert.Equal(PortfolioAuthorityOutcome.Unavailable, result.Authority.Outcome);
            Assert.True(server.AcceptedConnections > 0);
            Assert.Equal(0, server.Requests);
            Assert.Equal(0, server.CredentialRequests);
        }
        finally { await server.DisposeAsync(); }
        Assert.True(server.CleanupVerified);
    }

    [Fact]
    public async Task Trusted_tls_accepts_saved_ticket_and_redirect_never_reaches_destination()
    {
        var server = new PortfolioAuthTransportTestHost();
        var sink = new PortfolioAuthTransportTestHost();
        try
        {
            var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
            using var client = server.Client();
            var authority = server.Authority(client, tenant, actor, "run-owned-test-credential");
            var first = await authority.CandidatesAsync(Scope(tenant, actor), default);
            Assert.Equal(PortfolioAuthorityOutcome.Allowed, first.Authority.Outcome);
            Assert.Equal(1, server.Requests); Assert.Equal(1, server.CredentialRequests);

            server.Redirect = new Uri(sink.Origin, "api/users/lookup");
            var redirect = await authority.CandidatesAsync(Scope(tenant, actor), default);
            Assert.Equal(PortfolioAuthorityOutcome.Unavailable, redirect.Authority.Outcome);
            Assert.Equal(2, server.Requests);
            Assert.Equal(0, sink.AcceptedConnections);
            Assert.Equal(0, sink.Requests); Assert.Equal(0, sink.CredentialRequests);
        }
        finally { await server.DisposeAsync(); await sink.DisposeAsync(); }
        Assert.True(server.CleanupVerified); Assert.True(sink.CleanupVerified);
    }

    [Fact]
    public async Task Default_header_credentials_are_rejected_before_network()
    {
        await using var server = new PortfolioAuthTransportTestHost();
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        using var client = server.Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "poison-fixture-default");
        var result = await server.Authority(client, tenant, actor, "run-owned-test-credential").CandidatesAsync(Scope(tenant, actor), default);
        Assert.Equal(PortfolioAuthorityOutcome.Unavailable, result.Authority.Outcome);
        Assert.Equal(0, server.Requests); Assert.Equal(0, server.CredentialRequests);
    }

    private static PortfolioAuthorityScope Scope(Guid tenant, Guid actor)
    {
        var id = Guid.NewGuid();
        return new(tenant, actor, id, "owner-candidates", 1, Search: "", Limit: 5, RecordTenantId: tenant, CreatorId: actor,
            LifecycleState: Diten.PpmService.Domain.Entities.PortfolioLifecycleState.Draft,
            TemporaryNonProductionAccessBinding: new PortfolioTemporaryNonProductionRecordAccessAuthority(true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction).CreateBinding(id));
    }
}

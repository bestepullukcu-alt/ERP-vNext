using System.Net;
using System.Text;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Infrastructure.Portfolios;
using Xunit;

namespace Diten.PpmService.Tests.Portfolios;

public sealed class PortfolioAuthorityClientTests
{
    [Fact]
    public async Task Valid_named_label_is_separate_from_target_eligibility()
    {
        var targetId = Guid.NewGuid();
        var client = Client(request => request.RequestUri!.AbsolutePath.EndsWith("account-assertion", StringComparison.Ordinal)
            ? Assertion(targetId, "Human")
            : Json($"{{\"data\":{{\"userId\":\"{targetId:D}\",\"displayLabel\":\"Ada Owner\",\"labelState\":\"Named\"}},\"statusCode\":200,\"isSuccessful\":true}}"));

        var result = await client.EvaluateAsync(Scope(targetId), default);

        Assert.Equal(PortfolioAuthorityOutcome.Allowed, result.ActorAuthority.Outcome);
        Assert.Equal(PortfolioAuthorityOutcome.Allowed, result.TargetEligibility.Outcome);
        Assert.True(result.Active);
        Assert.True(result.NamedHuman);
        Assert.Equal(PortfolioOwnerLabelState.Available, result.LabelState);
        Assert.Equal("Ada Owner", result.DisplayLabel);
    }

    [Fact]
    public async Task Malformed_or_contradictory_label_state_is_unavailable_not_missing()
    {
        var targetId = Guid.NewGuid();
        var client = Client(request => request.RequestUri!.AbsolutePath.EndsWith("account-assertion", StringComparison.Ordinal)
            ? Assertion(targetId, "Human")
            : Json($"{{\"data\":{{\"userId\":\"{targetId:D}\",\"displayLabel\":null,\"labelState\":\"Named\"}},\"statusCode\":200,\"isSuccessful\":true}}"));

        var result = await client.EvaluateAsync(Scope(targetId), default);

        Assert.Equal(PortfolioAuthorityOutcome.Unavailable, result.TargetEligibility.Outcome);
        Assert.Equal(PortfolioOwnerLabelState.Available, result.LabelState);
    }

    [Fact]
    public async Task Unrelated_actor_is_denied_before_any_target_lookup()
    {
        var calls = 0;
        var client = Client(_ => { calls++; return Json("{}"); });
        var scope = Scope(Guid.NewGuid()) with { CreatorId = Guid.NewGuid() };

        var result = await client.EvaluateAsync(scope, default);

        Assert.Equal(PortfolioAuthorityOutcome.Denied, result.ActorAuthority.Outcome);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("not-a-guid", "200", "Human", "2026-09-13T00:00:00+00:00", "2026-09-13T00:00:00+00:00")]
    [InlineData("{target}", "999999999999", "Human", "2026-09-13T00:00:00+00:00", "2026-09-13T00:00:00+00:00")]
    [InlineData("{target}", "200", "Human", "2026-09-13T00:00:00+00:00", null)]
    [InlineData("{target}", "200", "Robot", "2026-09-13T00:00:00+00:00", "2026-09-13T00:00:00+00:00")]
    public async Task Malformed_assertion_shapes_are_controlled_unavailable(
        string id, string statusCode, string accountKind, string assertedAt, string? userUpdatedAt)
    {
        var targetId = Guid.NewGuid();
        var actualId = id == "{target}" ? targetId.ToString("D") : id;
        var updated = userUpdatedAt is null ? "" : $",\"userUpdatedAt\":\"{userUpdatedAt}\"";
        var client = Client(request => request.RequestUri!.AbsolutePath.EndsWith("account-assertion", StringComparison.Ordinal)
            ? Json($"{{\"data\":{{\"userId\":\"{actualId}\",\"active\":true,\"accountKind\":\"{accountKind}\",\"assertedAt\":\"{assertedAt}\"{updated}}},\"statusCode\":{statusCode},\"isSuccessful\":true}}")
            : throw new InvalidOperationException("Label must not be requested after malformed assertion."));

        var result = await client.EvaluateAsync(Scope(targetId), default);

        Assert.Equal(PortfolioAuthorityOutcome.Unavailable, result.TargetEligibility.Outcome);
    }

    [Fact]
    public async Task Unknown_or_contradictory_label_is_controlled_unavailable_and_404_is_definite_not_found()
    {
        var targetId = Guid.NewGuid();
        var unknown = Client(request => request.RequestUri!.AbsolutePath.EndsWith("account-assertion", StringComparison.Ordinal)
            ? Assertion(targetId, "Human")
            : Json($"{{\"data\":{{\"userId\":\"{targetId:D}\",\"displayLabel\":\"Ada\",\"labelState\":\"Mystery\"}},\"statusCode\":200,\"isSuccessful\":true}}"));
        Assert.Equal(PortfolioAuthorityOutcome.Unavailable, (await unknown.EvaluateAsync(Scope(targetId), default)).TargetEligibility.Outcome);

        var missing = Client(request => request.RequestUri!.AbsolutePath.EndsWith("account-assertion", StringComparison.Ordinal)
            ? Assertion(targetId, "Human")
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.Equal(PortfolioAuthorityOutcome.NotFound, (await missing.EvaluateAsync(Scope(targetId), default)).TargetEligibility.Outcome);
    }

    [Fact]
    public async Task Candidates_assert_account_kind_and_skip_valid_unselectable_labels()
    {
        var human = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        var service = Guid.NewGuid();
        var unnamed = Guid.NewGuid();
        var client = Client(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/lookup", StringComparison.Ordinal))
                return Json($"{{\"data\":[{{\"userId\":\"{human:D}\",\"displayLabel\":\"Ada\"}},{{\"userId\":\"{unknown:D}\",\"displayLabel\":\"Unknown\"}},{{\"userId\":\"{service:D}\",\"displayLabel\":\"Service\"}},{{\"userId\":\"{unnamed:D}\",\"displayLabel\":\"\"}}],\"statusCode\":200,\"isSuccessful\":true}}");
            var id = Guid.Parse(path.Split('/')[3]);
            if (path.EndsWith("account-assertion", StringComparison.Ordinal))
                return Assertion(id, id == human || id == unnamed ? "Human" : id == unknown ? "Unknown" : "Service");
            return id == unnamed
                ? Json($"{{\"data\":{{\"userId\":\"{id:D}\",\"displayLabel\":null,\"labelState\":\"Unnamed\"}},\"statusCode\":200,\"isSuccessful\":true}}")
                : Json($"{{\"data\":{{\"userId\":\"{id:D}\",\"displayLabel\":\"Ada\",\"labelState\":\"Named\"}},\"statusCode\":200,\"isSuccessful\":true}}");
        });

        var result = await client.CandidatesAsync(Scope(Guid.NewGuid()) with { Operation = "owner-candidates", Search = "", Limit = 4 }, default);

        Assert.Equal(PortfolioAuthorityOutcome.Allowed, result.Authority.Outcome);
        Assert.Equal(new[] { human }, result.Candidates.Select(x => x.UserId));
    }

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static PortfolioAuthorityClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        PortfolioAuthTransportTests.CreateClient(new StubHandler(responder), TenantId, ActorId);

    private static PortfolioAuthorityScope Scope(Guid targetId)
    {
        var tenantId = TenantId;
        var actorId = ActorId;
        var portfolioId = Guid.NewGuid();
        var binding = new PortfolioTemporaryNonProductionRecordAccessAuthority(
            true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction).CreateBinding(portfolioId);
        return new(tenantId, actorId, portfolioId, "assign-owner", 1, TargetUserId: targetId,
            RecordTenantId: tenantId, CreatorId: actorId,
            LifecycleState: Diten.PpmService.Domain.Entities.PortfolioLifecycleState.Draft,
            TemporaryNonProductionAccessBinding: binding);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Assertion(Guid targetId, string accountKind, bool active = true) => Json(
        $"{{\"data\":{{\"userId\":\"{targetId:D}\",\"active\":{active.ToString().ToLowerInvariant()},\"accountKind\":\"{accountKind}\",\"assertedAt\":\"2026-09-13T00:00:00+00:00\",\"userUpdatedAt\":null}},\"statusCode\":200,\"isSuccessful\":true}}");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}

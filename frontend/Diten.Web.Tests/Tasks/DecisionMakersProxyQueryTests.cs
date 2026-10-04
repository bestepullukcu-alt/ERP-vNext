using Diten.Web.Controllers;
using Xunit;

namespace Diten.Web.Tests.Tasks;

/// <summary>
/// BL-512 — the web proxy hands the people search upstream with ONLY its two parameters (search, ids), each
/// URL-encoded; whatever else a caller puts on the query string stays in the web tier.
/// </summary>
public sealed class DecisionMakersProxyQueryTests
{
    [Fact]
    public void Only_search_and_ids_travel_upstream_and_both_are_encoded()
    {
        Assert.Equal("?search=%C4%B0lker%20%26%20Co", TasksController.DecisionMakersQuery("İlker & Co", null));
        Assert.Equal("?ids=a%2Cb", TasksController.DecisionMakersQuery(null, "a,b"));
        Assert.Equal("?search=x&ids=y", TasksController.DecisionMakersQuery("x", "y"));
        Assert.Equal(string.Empty, TasksController.DecisionMakersQuery(null, null));
    }

    [Fact]
    public void The_proxy_action_binds_only_the_two_parameters()
    {
        var action = typeof(TasksController).GetMethod(nameof(TasksController.ApiDecisionMakers))!;
        Assert.Equal(new[] { "search", "ids" }, action.GetParameters().Select(p => p.Name));
    }
}

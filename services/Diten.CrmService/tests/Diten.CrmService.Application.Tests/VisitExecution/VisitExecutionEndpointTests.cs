using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>WP-VW-W3 (BE-a) — the §2.2 routes and their keys are FIXED (Web W3 and mobile depend on them): pinned here on
/// the production controller. No DELETE route exists.</summary>
public sealed class VisitExecutionEndpointTests
{
    [Theory]
    [InlineData("GET", "api/crm/visit-execution/contract", "crm.visit-execution.read")]
    [InlineData("GET", "api/crm/visit-execution/by-plan/{plannedVisitId:guid}", "crm.visit-execution.read")]
    [InlineData("GET", "api/crm/visit-execution/by-plan/{plannedVisitId:guid}/brief", "crm.visit-execution.read")]
    [InlineData("POST", "api/crm/visit-execution/start", "crm.visit-execution.record")]
    [InlineData("PUT", "api/crm/visit-execution/{id:guid}/capture", "crm.visit-execution.record")]
    [InlineData("POST", "api/crm/visit-execution/{id:guid}/finish", "crm.visit-execution.record")]
    [InlineData("POST", "api/crm/visit-execution/{id:guid}/signature", "crm.visit-execution.record")]
    [InlineData("GET", "api/crm/visit-execution/{id:guid}/photo", "crm.visit-execution.read")]
    [InlineData("GET", "api/crm/visit-execution/{id:guid}/signature", "crm.visit-execution.read")]
    // W3-BE-c (K-C4) — the policy GET is read with .read OR .manage: an in-action check, no single-key attribute
    // (pinned in VwW3AuthoringTests.The_policy_reads_need_read_or_manage).
    [InlineData("PUT", "api/crm/visit-evidence-policy", "crm.visit-evidence-policy.manage")]
    public void The_route_exists_with_its_key(string verb, string template, string key)
    {
        var action = Actions().SingleOrDefault(a => a.Verbs.Contains(verb) && a.Template == template);
        Assert.True(action.Method is not null, $"{verb} {template}");
        Assert.Equal(new[] { key }, action.Method!.GetCustomAttributes<HasPermissionAttribute>().Select(p => p.Permission));
    }

    [Fact]
    public void There_is_no_delete()
        => Assert.DoesNotContain(Actions(), a => a.Verbs.Contains("DELETE"));

    private static IReadOnlyList<(MethodInfo Method, string Template, IReadOnlyList<string> Verbs)> Actions()
        => typeof(VisitExecutionController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>()
                .Select(h => (m, h.Template ?? string.Empty, (IReadOnlyList<string>)h.HttpMethods.ToList())))
            .ToList();
}

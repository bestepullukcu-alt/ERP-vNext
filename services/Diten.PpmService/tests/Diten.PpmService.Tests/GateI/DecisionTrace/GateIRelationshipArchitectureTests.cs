using System.Reflection;
using Diten.PpmService.Api.Controllers;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.PpmService.Tests.GateI.DecisionTrace;

public sealed class GateIRelationshipArchitectureTests
{
    [Fact]
    public void Controllers_are_authorized_and_expose_only_exact_non_generic_routes()
    {
        var controllers = new[]
        {
            typeof(InvestmentCaseGateIReferencesController),
            typeof(BenefitCommitmentGateIReferencesController)
        };

        foreach (var controller in controllers)
        {
            Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
            var prefix = Assert.Single(controller.GetCustomAttributes<RouteAttribute>()).Template!;
            Assert.DoesNotContain("{kind", prefix, StringComparison.OrdinalIgnoreCase);

            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var route = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
                Assert.DoesNotContain("{kind", route.Template ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Exact_route_matrix_is_complete_and_has_no_aliases()
    {
        var routes = new[]
        {
            typeof(InvestmentCaseGateIReferencesController),
            typeof(BenefitCommitmentGateIReferencesController)
        }
        .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>())))
        .Select(attribute => $"{Assert.Single(attribute.HttpMethods)} {attribute.Template}")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

        Assert.Equal(14, routes.Length);
        Assert.Equal(14, routes.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("PUT governing-decision", routes);
        Assert.Contains("DELETE governing-decision", routes);
        Assert.Contains("POST supporting-decisions", routes);
        Assert.Contains("DELETE supporting-decisions/{referenceId:guid}", routes);
        Assert.Contains("PUT selected-budget-version", routes);
        Assert.Contains("POST scenario-versions", routes);
        Assert.Contains("POST comparator-outputs", routes);
        Assert.Contains("PUT selected-scenario", routes);
        Assert.Contains("POST outcomes", routes);
        Assert.Contains("DELETE outcomes/{referenceId:guid}", routes);
    }

    [Fact]
    public void Mutation_contract_has_no_client_supplied_security_context()
    {
        var properties = typeof(GateIRelationshipMutationCommand).GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("TenantId", properties);
        Assert.DoesNotContain("ActorId", properties);
        Assert.DoesNotContain("DelegatedActorId", properties);
        Assert.DoesNotContain("ServicePrincipalId", properties);
        Assert.DoesNotContain("Permission", properties);
        Assert.DoesNotContain("ModuleCode", properties);
    }

    [Fact]
    public void Gate_I_remains_zero_WorkCenter_projection_and_action_surface()
    {
        var root = FindRepositoryRoot();
        var roots = new[]
        {
            Path.Combine(root, "services", "Diten.PpmService", "src"),
            Path.Combine(root, "frontend", "Diten.Web", "Controllers", "PPM"),
            Path.Combine(root, "frontend", "Diten.Web", "Views", "PPM"),
            Path.Combine(root, "frontend", "Diten.Web", "wwwroot", "assets", "js", "pages", "ppm")
        };
        var forbidden = new[]
        {
            "IWorkItemProvider",
            "IWorkItemActionDispatcher",
            "/api/v1/work-items/projection",
            "/api/v1/work-items/{itemId}/actions",
            "WorkCenterNext"
        };

        foreach (var file in roots.Where(Directory.Exists).SelectMany(path =>
                     Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                         .Where(candidate => candidate.EndsWith(".cs", StringComparison.Ordinal)
                             || candidate.EndsWith(".cshtml", StringComparison.Ordinal)
                             || candidate.EndsWith(".js", StringComparison.Ordinal))))
        {
            var source = File.ReadAllText(file);
            foreach (var token in forbidden)
                Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}

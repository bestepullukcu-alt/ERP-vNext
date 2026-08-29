using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Features.Territory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

/// <summary>Repository-level FU02B scope guard: lifecycle POST actions are allowed, while workflow approval,
/// assignments, rules, resource, evidence, import/export and hard DELETE stay absent.</summary>
public sealed class TerritoryScopeGuardTests
{
    [Fact]
    public void Permissions_Are_Exactly_The_Five_Fu01_Keys()
    {
        Assert.Equal(new[]
        {
            "crm.territory.read",
            "crm.territory.model.read",
            "crm.territory.model.manage",
            "crm.territory.node.read",
            "crm.territory.node.manage"
        }, TerritoryPermissions.All);
    }

    [Fact]
    public void Superseded_Permissions_Are_Not_Defined()
    {
        Assert.DoesNotContain("crm.micro-zone.manage", TerritoryPermissions.All);
        Assert.DoesNotContain("crm.territory.delete", TerritoryPermissions.All);
        Assert.DoesNotContain(TerritoryPermissions.All, p => p.Contains("assign-rep") || p.Contains("assign-account"));
    }

    [Fact]
    public void TerritoryModelsController_Exposes_No_Delete_Endpoint()
    {
        var deletes = RouteTemplates(typeof(TerritoryModelsController), typeof(HttpDeleteAttribute));
        Assert.Empty(deletes);
    }

    [Fact]
    public void TerritoryModelsController_Exposes_No_OutOfScope_Endpoints()
    {
        // FU05 adds account assignment apply/history; FU08 adds import/export. Workflow, approval, evidence and
        // coverage roll-up remain absent.
        var forbidden = new[]
        {
            "supersede", "submit-approval", "approve", "reject", "approval-trace", "evidence", "coverage-rollup"
        };

        var templates = RouteTemplates(typeof(TerritoryModelsController));
        foreach (var template in templates)
        {
            foreach (var bad in forbidden)
            {
                Assert.DoesNotContain(bad, template, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void TerritoryModelsController_Exposes_Fu08_ImportExport_Endpoints()
    {
        var templates = RouteTemplates(typeof(TerritoryModelsController));

        Assert.Contains("{id:guid}/export", templates);
        Assert.Contains("{id:guid}/import-template", templates);
        Assert.Contains("{id:guid}/import-file", templates);
        Assert.Contains("{id:guid}/import-file/apply", templates);
        Assert.Contains("{id:guid}/import-runs", templates);
    }

    [Fact]
    public void Fu08_Opens_No_Resource_Import_Apply_Route()
    {
        // The FU04A lifecycle must not be reachable through a file. There is no route, and no apply path in the
        // engine either — the sheet is dry-run only until FU08A is separately authorized.
        var templates = RouteTemplates(typeof(TerritoryModelsController));

        Assert.DoesNotContain(templates, t => t.Contains("resource-import", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(templates, t => t.Contains("import-resources", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Fu08_Opens_No_New_Seeded_Permission()
    {
        // The canonical export/import keys are DEFINED but stay out of the advertised catalog until FU08-RBAC.
        Assert.Equal(5, TerritoryPermissions.All.Count);
        Assert.DoesNotContain(TerritoryPermissions.Export, TerritoryPermissions.All);
        Assert.DoesNotContain(TerritoryPermissions.Import, TerritoryPermissions.All);
    }

    [Fact]
    public void TerritoryModelsController_Exposes_Fu03_Rule_And_Preview_Endpoints()
    {
        var templates = RouteTemplates(typeof(TerritoryModelsController));

        Assert.Contains("{id:guid}/assignment-rules", templates);
        Assert.Contains("{id:guid}/assignment-rules/{ruleId:guid}", templates);
        Assert.Contains("{id:guid}/assignment-rules/{ruleId:guid}/delete-draft", templates);
        Assert.Contains("{id:guid}/assignment-preview", templates);
    }

    [Fact]
    public void Fu03_Opens_No_New_Permission()
    {
        // FU03 reuses model.read / model.manage exactly like FU02B reused them for lifecycle (pack §22.1).
        Assert.Equal(5, TerritoryPermissions.All.Count);
        Assert.DoesNotContain("crm.territory.assignment.apply", TerritoryPermissions.All);
        Assert.DoesNotContain("crm.territory.resource.manage", TerritoryPermissions.All);
        Assert.DoesNotContain("crm.territory.delete", TerritoryPermissions.All);
        Assert.DoesNotContain("crm.micro-zone.manage", TerritoryPermissions.All);
    }

    [Fact]
    public void Fu05_AccountTerritoryAssignment_Exists_Without_Workflow_Or_Evidence_Aggregates()
    {
        // The strongest apply guard available: FU05's aggregate simply does not exist in the domain assembly, so no
        // handler can persist one by accident. FU04's TerritoryResourceAssignment (people) is expected to be present —
        // assigning a PERSON and assigning an ACCOUNT are different things, and only the latter is still forbidden.
        var domainTypes = typeof(Diten.CrmService.Domain.Entities.TerritoryAssignmentRule).Assembly.GetTypes();

        Assert.Contains(domainTypes, t => t.Name == "AccountTerritoryAssignment");
        Assert.DoesNotContain(domainTypes, t => t.Name.Contains("TerritoryChangeRequest", StringComparison.Ordinal));
        Assert.DoesNotContain(domainTypes, t => t.Name.Contains("TerritoryEvidencePack", StringComparison.Ordinal));

        Assert.Contains(domainTypes, t => t.Name == "TerritoryResourceAssignment");
    }

    [Fact]
    public void TerritoryModelsController_Exposes_Fu05_Account_Assignment_Endpoints()
    {
        var templates = RouteTemplates(typeof(TerritoryModelsController));
        Assert.Contains("{id:guid}/account-assignments", templates);
        Assert.Contains("{id:guid}/account-assignments/{assignmentId:guid}", templates);
        Assert.Contains("{id:guid}/assignment-preview/apply", templates);
        Assert.Contains("{id:guid}/account-assignments/{assignmentId:guid}/end", templates);
    }

    [Fact]
    public void TerritoryModelsController_Exposes_Fu04_Resource_Assignment_Endpoints()
    {
        var templates = RouteTemplates(typeof(TerritoryModelsController));

        Assert.Contains("{id:guid}/resource-assignments", templates);
        Assert.Contains("{id:guid}/resource-assignments/{assignmentId:guid}", templates);
        Assert.Contains("{id:guid}/resource-assignments/{assignmentId:guid}/delete-draft", templates);
        Assert.Contains("{id:guid}/resource-assignments/{assignmentId:guid}/end", templates);
        Assert.Contains("{id:guid}/resource-assignments/validate-conflicts", templates);
    }

    [Fact]
    public void Fu04_Stores_No_Employee_Master()
    {
        // MOD-0151 references a person, it does not own one (pack §10). The only resource type in the domain is the
        // display/id seam; an Employee/Person aggregate appearing here would be a boundary break.
        var domainTypes = typeof(Diten.CrmService.Domain.Entities.TerritoryResourceAssignment).Assembly.GetTypes();

        Assert.DoesNotContain(domainTypes, t => t.Name is "Employee" or "Person" or "Position");
        Assert.Contains(domainTypes, t => t.Name == "TerritoryResourceRef");
    }

    [Fact]
    public void Territory_Account_Reader_Seam_Has_No_Mutating_Member()
    {
        // The preview consumes accounts through a read-only seam; if someone adds a write here the guard fails.
        var members = typeof(Diten.CrmService.Domain.Repositories.ITerritoryAccountReader).GetMethods();

        Assert.All(members, m => Assert.DoesNotContain(
            new[] { "Insert", "Update", "Delete", "Save", "Write", "Upsert" },
            verb => m.Name.Contains(verb, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void TerritoryModelsController_Exposes_Only_Fu02B_Lifecycle_Post_Actions()
    {
        var templates = RouteTemplates(typeof(TerritoryModelsController));
        Assert.Contains("{id:guid}/activate", templates);
        Assert.Contains("{id:guid}/deactivate", templates);
        Assert.Contains("{id:guid}/archive", templates);
        Assert.Contains("{id:guid}/delete-draft", templates);
        Assert.Contains("{id:guid}/nodes/{nodeId:guid}/delete-draft", templates);
    }

    private static IReadOnlyList<string> RouteTemplates(Type controller, Type? httpMethodAttribute = null)
    {
        var templates = new List<string>();
        foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var attr in method.GetCustomAttributes())
            {
                if (attr is not IRouteTemplateProvider route)
                {
                    continue;
                }

                if (httpMethodAttribute is not null && attr.GetType() != httpMethodAttribute)
                {
                    continue;
                }

                templates.Add(route.Template ?? string.Empty);
            }
        }

        return templates;
    }
}

using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Ppm.SelfRegistration;

public sealed class PpmManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "PPM",
            ModuleName: "Project & Portfolio Management",
            DisplayName: "Project & Portfolio Management",
            Domain: "Portfolio Delivery",
            Service: "Diten.PpmService",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 70,
            Icon: "bx-briefcase-alt-2",
            IsBaseline: false,
            Pages:
            [
                new ModuleManifestPage("PORTFOLIOS", "Portfolios", "/ppm/portfolios", "ppm.portfolios.read", null, true, "List", 10,
                [
                    new("CREATE", "Create Portfolio", "ppm.portfolios.create", "Toolbar", 10, false, true, false),
                    new("EDIT", "Edit Portfolio", "ppm.portfolios.update", "RowAction", 20, false, false, true),
                    new("CHANGE_LIFECYCLE", "Change Portfolio Lifecycle", "ppm.portfolios.change-lifecycle", "RowAction", 30, false, false, true)
                ]),
                new ModuleManifestPage("INITIATIVES", "Initiatives", "/ppm/initiatives", "ppm.initiatives.read", null, true, "List", 20,
                [
                    new("CREATE", "Create Initiative", "ppm.initiatives.create", "Toolbar", 10, false, true, false),
                    new("EDIT", "Edit Initiative", "ppm.initiatives.update", "RowAction", 20, false, false, true),
                    new("CHANGE_LIFECYCLE", "Change Initiative Lifecycle", "ppm.initiatives.change-lifecycle", "RowAction", 30, false, false, true)
                ]),
                new ModuleManifestPage("PROGRAMS", "Programs", "/ppm/programs", "ppm.programs.read", null, true, "List", 30,
                [
                    new("CREATE", "Create Program", "ppm.programs.create", "Toolbar", 10, false, true, false),
                    new("EDIT", "Edit Program", "ppm.programs.update", "RowAction", 20, false, false, true),
                    new("CHANGE_LIFECYCLE", "Change Program Lifecycle", "ppm.programs.change-lifecycle", "RowAction", 30, false, false, true)
                ]),
                new ModuleManifestPage("PROJECTS", "Projects", "/ppm/projects", "ppm.projects.read", null, true, "List", 40,
                [
                    new("CREATE", "Create Project", "ppm.projects.create", "Toolbar", 10, false, true, false),
                    new("EDIT", "Edit Project", "ppm.projects.update", "RowAction", 20, false, false, true),
                    new("CHANGE_LIFECYCLE", "Change Project Lifecycle", "ppm.projects.change-lifecycle", "RowAction", 30, false, false, true)
                ]),
                new ModuleManifestPage("INVESTMENT_CASES", "Investment Cases", "/ppm/investment-cases", "ppm.investment-cases.read", null, true, "List", 50,
                [
                    new("CREATE", "Create Investment Case", "ppm.investment-cases.create", "Toolbar", 10, false, true, false),
                    new("EDIT", "Edit Investment Case", "ppm.investment-cases.update", "RowAction", 20, false, false, true),
                    new("CHANGE_LIFECYCLE", "Change Investment Case Lifecycle", "ppm.investment-cases.change-lifecycle", "RowAction", 30, false, false, true)
                ]),
                new ModuleManifestPage("BENEFIT_COMMITMENTS", "Benefit Commitments", "/ppm/benefit-commitments", "ppm.benefit-commitments.read", null, true, "List", 60,
                [
                    new("CREATE", "Create Benefit Commitment", "ppm.benefit-commitments.create", "Toolbar", 10, false, true, false),
                    new("EDIT", "Edit Benefit Commitment", "ppm.benefit-commitments.update", "RowAction", 20, false, false, true),
                    new("CHANGE_LIFECYCLE", "Change Benefit Commitment Lifecycle", "ppm.benefit-commitments.change-lifecycle", "RowAction", 30, false, false, true)
                ])
            ]);
}

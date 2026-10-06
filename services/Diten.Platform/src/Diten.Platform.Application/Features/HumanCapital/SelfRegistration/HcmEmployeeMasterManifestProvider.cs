using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.HumanCapital.SelfRegistration;

/// <summary>
/// MOD-0251 Employee Master — the DitenHcmService employee-master module self-registration manifest (nav wiring gap #4).
/// <para>A DISTINCT module from <see cref="HumanCapitalManifestProvider"/> (HUMAN-CAPITAL): the employee master is owned
/// by <c>DitenHcmService</c> (port 5060) with its own <c>mod0251.*</c> permission namespace, so it carries its own
/// ModuleCode <c>HCM-EMPLOYEE-MASTER</c> rather than folding into the Human Capital catalog entry. <c>IModuleManifestProvider</c>
/// yields one document per provider, so a separate provider is the only way to register a second ModuleCode.</para>
/// <para>The single page mirrors the Diten.Web <c>Route("HCM/Employees")</c> controller; its read permission and its
/// draft-authoring action are the verbatim keys the DitenHcmService <c>EmployeesController</c> /
/// <c>EmployeeDraftsController</c> <c>[HasPermission]</c> enforce. Only the employee-master list page is registered —
/// the smoke-fixture/draft-submit endpoints are not navigation surfaces. Entitlement + RBAC grant (WP-C) and the
/// seven-language <c>Nav.Page.*</c> labels (WP-D) are separate work packages.</para>
/// </summary>
public sealed class HcmEmployeeMasterManifestProvider : IModuleManifestProvider
{
    // Verbatim keys enforced by DitenHcmService (EmployeesController.ViewPermission / EmployeeDraftsController.DraftPermission).
    private const string EmployeeView = "mod0251.employee.view";
    private const string EmployeeCreateDraft = "mod0251.employee.create_draft";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "HCM-EMPLOYEE-MASTER",
            ModuleName: "Employee Master",
            DisplayName: "Employee Master",
            Domain: "Human Capital", // SOFT: only seeds on first-register; operator-owned thereafter.
            Service: "DitenHcmService", // SOFT.
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true, // SOFT.
            SortOrder: 75,
            Pages:
            [
                new ModuleManifestPage("EMPLOYEE_MASTER", "Employee Master", "/HCM/Employees", EmployeeView, null, true, "List", 10,
                [
                    new ModuleManifestAction("CREATE_DRAFT", "New Employee Draft", EmployeeCreateDraft, "Toolbar", 10, false, true, false)
                ])
            ],
            Icon: "bx-id-card",
            IsBaseline: false); // HARD: MOD-0251 is a licensed module — tenant entitlement required.
}

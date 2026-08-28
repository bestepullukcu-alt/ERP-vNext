using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.WorkingCalendarImport;

public sealed class WorkingCalendarImportManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() => new(
        ModuleCode: "working-calendar-import",
        ModuleName: "Working Calendar Import",
        DisplayName: "Working Calendar Imports",
        Domain: "PlatformSharedServices",
        Service: "DitenPlatform",
        ModuleVersion: "1.0.0",
        IsTenantAssignable: false,
        SortOrder: 91,
        Icon: "bx-cloud-download",
        IsBaseline: true,
        Pages:
        [
            new ModuleManifestPage("WORKING_CALENDAR_IMPORTS", "Working Calendar Imports",
                "/Platform/WorkingCalendarImports", WorkingCalendarImportPermissionKeys.Read, null, true, "List", 10,
                [
                    new ModuleManifestAction("WCI_RUN", "Start Import", WorkingCalendarImportPermissionKeys.Run, "Create", 10, false, true, false),
                    new ModuleManifestAction("WCI_REVIEW", "Review or Discard", WorkingCalendarImportPermissionKeys.Review, "Review", 20, false, false, true),
                    new ModuleManifestAction("WCI_APPLY", "Apply", WorkingCalendarImportPermissionKeys.Apply, "Apply", 30, false, false, true)
                ]),
            new ModuleManifestPage("WORKING_CALENDAR_IMPORT_REVIEW", "Review Working Calendar Import",
                "/Platform/WorkingCalendarImports/Review", WorkingCalendarImportPermissionKeys.Read,
                "WORKING_CALENDAR_IMPORTS", false, "Details", 20,
                [
                    new ModuleManifestAction("WCI_REVIEW_DECISION", "Review or Discard", WorkingCalendarImportPermissionKeys.Review, "Review", 10, false, false, true),
                    new ModuleManifestAction("WCI_REVIEW_APPLY", "Apply", WorkingCalendarImportPermissionKeys.Apply, "Apply", 20, false, false, true)
                ])
        ]);
}

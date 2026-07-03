using System.ComponentModel.DataAnnotations;

namespace Diten.Platform.Domain.Enums;

public enum ModuleCatalogDomain
{
    [Display(Name = "Platform Shared Services")]
    PlatformSharedServices,

    [Display(Name = "PPM Management")]
    PpmManagement,

    [Display(Name = "Master Data Management")]
    MasterDataManagement,

    [Display(Name = "Quality Management")]
    QualityManagement,

    [Display(Name = "Research Management")]
    ResearchManagement,

    [Display(Name = "Document Management")]
    DocumentManagement,

    [Display(Name = "Finance")]
    Finance,

    [Display(Name = "Sales")]
    Sales,

    [Display(Name = "Inventory")]
    Inventory,

    [Display(Name = "Production")]
    Production,

    [Display(Name = "HR")]
    Hr,

    [Display(Name = "Developer Enablement")]
    DevEnablement,

    // FEAT-BASELINE-MODULES-S1 — domain for the Access Governance baseline module (Users/Roles/Permissions/…).
    [Display(Name = "Access Governance")]
    AccessGovernance,

    // FEAT-BASELINE-MODULES-S2 — domain for the Tenant Settings baseline module (Security Settings / Menu Settings).
    [Display(Name = "Settings")]
    Settings,

    // FEAT-ADMIN-DOMAIN — unified tenant-administration domain (Access Governance + Tenant Settings). The older
    // AccessGovernance/Settings values are retained (harmless — no module uses them now; operator may retire them).
    [Display(Name = "Administration")]
    Administration
}

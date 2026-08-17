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
    Hr
}

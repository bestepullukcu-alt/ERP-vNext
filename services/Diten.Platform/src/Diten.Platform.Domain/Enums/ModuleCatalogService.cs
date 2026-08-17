using System.ComponentModel.DataAnnotations;

namespace Diten.Platform.Domain.Enums;

public enum ModuleCatalogService
{
    [Display(Name = "Diten.Platform")]
    DitenPlatform,

    [Display(Name = "Diten.AuthService")]
    DitenAuthService,

    [Display(Name = "Diten.Platform.Common")]
    DitenPlatformCommon,

    [Display(Name = "Diten.PpmService")]
    DitenPpmService,

    [Display(Name = "Diten.MdmService")]
    DitenMdmService,

    [Display(Name = "Diten.QualityService")]
    DitenQualityService,

    [Display(Name = "Diten.ResearchService")]
    DitenResearchService,

    [Display(Name = "Diten.DocumentService")]
    DitenDocumentService
}

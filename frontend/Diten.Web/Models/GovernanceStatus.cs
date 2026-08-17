using System.ComponentModel.DataAnnotations;

namespace WebUI.Models;

public enum GovernanceStatus
{
    [Display(Name = "Healthy")]
    Healthy = 0,
    [Display(Name = "Monitor")]
    Monitor = 1,
    [Display(Name = "At Risk")]
    AtRisk = 2,
    [Display(Name = "Critical")]
    Critical = 3,
    [Display(Name = "Overstock")]
    Overstock = 4
}

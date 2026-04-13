using System.ComponentModel.DataAnnotations;

namespace WebUI.Models;

public enum AgingBucket
{
    [Display(Name = "All Aging Buckets")]
    All = 0,
    [Display(Name = "0-30")]
    B0_30 = 1,
    [Display(Name = "31-60")]
    B31_60 = 2,
    [Display(Name = "61-90")]
    B61_90 = 3,
    [Display(Name = "90+")]
    B90Plus = 4
}

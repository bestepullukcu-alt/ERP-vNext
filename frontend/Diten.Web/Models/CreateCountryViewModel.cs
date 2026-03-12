using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models;

public sealed class CreateCountryViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string Iso2Code { get; set; } = string.Empty;

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Iso3Code { get; set; } = string.Empty;

    public string? PhoneCode { get; set; }

    public bool IsActive { get; set; } = true;
}


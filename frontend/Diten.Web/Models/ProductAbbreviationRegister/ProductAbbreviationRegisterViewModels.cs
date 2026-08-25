using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.ProductAbbreviationRegister;

public sealed class RequestProductAbbreviationViewModel
{
    [Required]
    public Guid GlobalProductId { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Abbreviation { get; set; } = string.Empty;
}

public sealed record ProductAbbreviationDecisionViewModel(
    int ExpectedVersion,
    string? Reason = null,
    int? ExpectedFormerVersion = null);

public sealed record ProductAbbreviationCorrectionViewModel(
    int ExpectedVersion,
    string ReplacementAbbreviation,
    string Reason);

public sealed record ProductAbbreviationRetirementViewModel(int ExpectedVersion, string Reason);

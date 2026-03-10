using System.ComponentModel.DataAnnotations;

namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// Country entity for managing countries with ISO codes and phone codes.
/// Inherits TenantId and Soft-Delete from EntityBase.
/// </summary>
public class Country : EntityBase
{
    /// <summary>
    /// Country name in the system language (e.g., Turkey, Germany).
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Country name in its native language (e.g., Türkiye, Deutschland).
    /// </summary>
    public string? NativeName { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 code (e.g., TR, US, DE).
    /// </summary>
    [Required]
    public string Iso2Code { get; set; } = string.Empty;

    /// <summary>
    /// ISO 3166-1 alpha-3 code (e.g., TUR, USA, DEU).
    /// </summary>
    [Required]
    public string Iso3Code { get; set; } = string.Empty;

    /// <summary>
    /// ISO 3166-1 numeric code (e.g., 792, 840, 276).
    /// </summary>
    public string? NumericCode { get; set; }

    /// <summary>
    /// International telephone calling code (e.g., +90, +1, +49).
    /// </summary>
    public string? PhoneCode { get; set; }

    /// <summary>
    /// Default currency ISO code (e.g., TRY, USD, EUR).
    /// </summary>
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Currency name (e.g., Turkish Lira, US Dollar).
    /// </summary>
    public string? CurrencyName { get; set; }

    /// <summary>
    /// Currency symbol (e.g., ₺, $, €).
    /// </summary>
    public string? CurrencySymbol { get; set; }

    /// <summary>
    /// Geographical region (e.g., Europe, Asia, Middle East).
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Sub-region (e.g., Western Europe, Southeast Asia).
    /// </summary>
    public string? SubRegion { get; set; }

    /// <summary>
    /// Capital city of the country.
    /// </summary>
    public string? Capital { get; set; }

    /// <summary>
    /// Flag emoji (e.g., 🇹🇷, 🇺🇸, 🇩🇪).
    /// </summary>
    public string? FlagEmoji { get; set; }

    /// <summary>
    /// Geographic latitude coordinate.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Geographic longitude coordinate.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Active/Passive status of the country record.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
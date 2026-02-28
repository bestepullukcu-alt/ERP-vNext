using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

public class LegalEntity : EntityBase
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string TaxOffice { get; set; } = string.Empty;

    [Required]
    public string TaxNumber { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    [BsonRepresentation(BsonType.String)]
    public Guid? CreatedBy { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? UpdatedBy { get; set; }

    // Additional requested fields
    public string? CompanyType { get; set; }
    public string? Sector { get; set; }
    public string? ContactPerson { get; set; }
    public string? PrimaryCurrency { get; set; }
    public string? DefaultTimeZone { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? ParentLegalEntityId { get; set; }

    public string? DefaultCommunicationLanguage { get; set; }
    public string? OrganizationRole { get; set; }
    public string? LogoUrl { get; set; }

    // Fiscal year start could be something like "01-01" or full date. Using string for flexibility or DateTime.
    public string? FiscalYearStart { get; set; }

    public DateTimeOffset? RegistrationDate { get; set; }
    public DateTimeOffset? EffectiveFromDate { get; set; }
    public string? TaxJurisdiction { get; set; }
}

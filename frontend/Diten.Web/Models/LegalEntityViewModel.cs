using System;
using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models
{
    public class LegalEntityViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string TaxNumber { get; set; }
        public string TaxOffice { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? CompanyType { get; set; }
    }

    public class CreateLegalEntityViewModel
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "FieldRequired")]
        [Display(Name = "Company Title")]
        [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "FieldRequired")]
        [Display(Name = "Tax Office")]
        public string TaxOffice { get; set; }

        [Required(ErrorMessage = "FieldRequired")]
        [Display(Name = "Tax Number")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "NumericOnly")]
        [StringLength(20, MinimumLength = 10, ErrorMessage = "Tax Number must be between 10 and 20 digits")]
        public string TaxNumber { get; set; }

        [EmailAddress(ErrorMessage = "InvalidEmail")]
        [Display(Name = "Email Address")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string? Email { get; set; }

        [Display(Name = "Phone Number")]
        [Phone(ErrorMessage = "InvalidPhone")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? Phone { get; set; }

        [Url(ErrorMessage = "InvalidUrl")]
        [Display(Name = "Website")]
        [StringLength(500, ErrorMessage = "Website URL cannot exceed 500 characters")]
        public string? Website { get; set; }

        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "Company Type")]
        public string? CompanyType { get; set; }

        [Display(Name = "Sector")]
        public string? Sector { get; set; }

        [Display(Name = "Contact Person")]
        public string? ContactPerson { get; set; }

        [Display(Name = "Primary Currency")]
        public string? PrimaryCurrency { get; set; }

        [Display(Name = "Default Time Zone")]
        public string? DefaultTimeZone { get; set; }

        [Display(Name = "Parent Legal Entity (ID)")]
        public Guid? ParentLegalEntityId { get; set; }

        [Display(Name = "Default Communication Language")]
        public string? DefaultCommunicationLanguage { get; set; }

        [Display(Name = "Organization Role")]
        public string? OrganizationRole { get; set; }

        [Url(ErrorMessage = "InvalidUrl")]
        [Display(Name = "Logo URL")]
        [StringLength(500, ErrorMessage = "Logo URL cannot exceed 500 characters")]
        public string? LogoUrl { get; set; }

        [Display(Name = "Fiscal Year Start")]
        [RegularExpression(@"^(0[1-9]|[12][0-9]|3[01])-(0[1-9]|1[012])$", ErrorMessage = "InvalidFiscalYear")]
        public string? FiscalYearStart { get; set; }

        [Display(Name = "Registration Date")]
        public DateTimeOffset? RegistrationDate { get; set; }

        [Display(Name = "Effective From Date")]
        public DateTimeOffset? EffectiveFromDate { get; set; }

        [Display(Name = "Tax Jurisdiction")]
        public string? TaxJurisdiction { get; set; }
    }
}

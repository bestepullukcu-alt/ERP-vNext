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
        [Required(ErrorMessage = "Title is required")]
        [Display(Name = "Company Title")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Tax Office is required")]
        [Display(Name = "Tax Office")]
        public string TaxOffice { get; set; }

        [Required(ErrorMessage = "Tax Number is required")]
        [Display(Name = "Tax Number")]
        public string TaxNumber { get; set; }

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string? Email { get; set; }

        [Display(Name = "Phone Number")]
        public string? Phone { get; set; }

        [Display(Name = "Website")]
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

        [Display(Name = "Logo URL")]
        public string? LogoUrl { get; set; }

        [Display(Name = "Fiscal Year Start")]
        public string? FiscalYearStart { get; set; }

        [Display(Name = "Registration Date")]
        public DateTimeOffset? RegistrationDate { get; set; }

        [Display(Name = "Effective From Date")]
        public DateTimeOffset? EffectiveFromDate { get; set; }

        [Display(Name = "Tax Jurisdiction")]
        public string? TaxJurisdiction { get; set; }
    }
}

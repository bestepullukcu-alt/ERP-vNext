using System.Text.Json;
using FluentValidation;

namespace Diten.MdmService.Application.Features.LegalEntity.Validators;

// MOD-0220 FULL — shared field validation for both Create and Update. Address-JSON well-formedness and the
// branch/rep-office parent rule live here so the two write paths cannot drift.
public sealed class LegalEntityWriteRequestValidator : AbstractValidator<LegalEntityWriteRequest>
{
    public LegalEntityWriteRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DisplayName).MaximumLength(256);
        RuleFor(x => x.LegalFormCode).NotEmpty().MaximumLength(64);
        // MOD-0220 finish — OrganizationRole (Structure) is deferred from the UI; optional now (mapping defaults it
        // to LEGALENTITY). Length-guarded only.
        RuleFor(x => x.OrganizationRoleCode).MaximumLength(64);

        RuleFor(x => x.RegistrationNumber).MaximumLength(128);
        RuleFor(x => x.TaxId).MaximumLength(128);
        // MOD-0220 finish — additive statutory fields. Free strings (no VIES/format validation), dates unconstrained.
        RuleFor(x => x.VatNumber).MaximumLength(64);
        RuleFor(x => x.PlaceOfIncorporation).MaximumLength(256);
        RuleFor(x => x.CountryCode).NotEmpty().MaximumLength(3);

        RuleFor(x => x.OwnershipPercent)
            .InclusiveBetween(0m, 100m)
            .When(x => x.OwnershipPercent.HasValue);
        RuleFor(x => x.ControlTypeCode).MaximumLength(64);

        RuleFor(x => x.FiscalYearVariant).MaximumLength(64);
        RuleFor(x => x.AccountingStandardCode).MaximumLength(64);
        RuleFor(x => x.TaxRegimeCode).MaximumLength(64);
        RuleFor(x => x.BaseCurrencyCode).NotEmpty().MaximumLength(3);

        // MOD-0220 finish — the registered-address sub-form is DEFERRED, so the address is optional now. When
        // supplied it must still be well-formed (line1/city/country); when omitted it simply isn't captured yet.
        RuleFor(x => x.RegisteredAddressJson)
            .Must(BeValidAddressJson)
            .When(x => !string.IsNullOrWhiteSpace(x.RegisteredAddressJson))
            .WithMessage("Registered address must be valid JSON containing non-empty line1, city and country.");
        RuleFor(x => x.CorrespondenceAddressJson)
            .Must(json => BeValidJsonObject(json))
            .When(x => !string.IsNullOrWhiteSpace(x.CorrespondenceAddressJson))
            .WithMessage("Correspondence address must be a valid JSON object.");

        RuleFor(x => x.OfficialEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.OfficialEmail));
        RuleFor(x => x.OfficialPhone).MaximumLength(64);
        RuleFor(x => x.Website).MaximumLength(256);

        RuleFor(x => x.SourceSystem).MaximumLength(128);
        RuleFor(x => x.LegacyCode).MaximumLength(128);

        // Plan rule: a Branch or Rep Office must declare its owning parent.
        RuleFor(x => x.ParentLegalEntityId)
            .Must(id => id.HasValue && id.Value != Guid.Empty)
            .When(x => LegalEntityRules.RequiresParent(x.OrganizationRoleCode))
            .WithMessage("A Branch or Rep Office must reference a parent Legal Entity.");
    }

    private static bool BeValidAddressJson(string? json)
    {
        if (!TryParseObject(json, out var root))
        {
            return false;
        }

        return HasNonEmpty(root, "line1") && HasNonEmpty(root, "city") && HasNonEmpty(root, "country");
    }

    private static bool BeValidJsonObject(string? json) => TryParseObject(json, out _);

    private static bool TryParseObject(string? json, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            root = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasNonEmpty(JsonElement root, string property)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, property, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(prop.Value.GetString());
            }
        }

        return false;
    }
}

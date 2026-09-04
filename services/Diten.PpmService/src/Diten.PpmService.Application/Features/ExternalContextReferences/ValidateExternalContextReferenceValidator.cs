using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.PpmService.Application.Features.ExternalContextReferences;


public sealed class ValidateExternalContextReferenceValidator
    : AbstractValidator<ValidateExternalContextReferenceQuery>
{
    public ValidateExternalContextReferenceValidator()
    {
        RuleFor(x => x.ContractName).Equal(ExternalContextReferenceContract.Name);
        RuleFor(x => x.ContractVersion).Equal(ExternalContextReferenceContract.Version);
        RuleFor(x => x.ContextKind)
            .Must(value => value is not null && ExternalContextReferenceContract.PermissionFor(value) is not null)
            .WithMessage("ContextKind is invalid.");
        RuleFor(x => x.ContextId)
            .Must(IsCanonicalNonEmptyGuid)
            .WithMessage("ContextId must be a canonical non-empty Guid.");
        RuleFor(x => x.HasAdditionalProperties).Equal(false)
            .WithMessage("Unknown contract fields are not allowed.");
    }

    private static bool IsCanonicalNonEmptyGuid(string? value) =>
        value is not null &&
        Guid.TryParseExact(value, "D", out var parsed) &&
        parsed != Guid.Empty &&
        string.Equals(parsed.ToString("D"), value, StringComparison.Ordinal);
}

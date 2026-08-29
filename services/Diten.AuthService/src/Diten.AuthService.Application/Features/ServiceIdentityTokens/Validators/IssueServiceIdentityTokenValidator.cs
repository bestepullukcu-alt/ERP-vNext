using Diten.AuthService.Application.Features.ServiceIdentityTokens.Commands;
using FluentValidation;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Validators;

public sealed class IssueServiceIdentityTokenValidator : AbstractValidator<IssueServiceIdentityTokenCommand>
{
    private const string RequiredAudience = "TRUSTED_AUDIT_SOURCE_INGEST";

    public IssueServiceIdentityTokenValidator()
    {
        RuleFor(x => x.ClientCode).NotEmpty().MaximumLength(128).Must(IsExactValue);
        RuleFor(x => x.ClientSecret).NotEmpty().MaximumLength(512).Must(IsExactValue);
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Audience).NotEmpty().MaximumLength(128).Must(IsExactValue)
            .Equal(RequiredAudience, StringComparer.Ordinal);
    }

    private static bool IsExactValue(string value) =>
        string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl)
        && !value.Contains(',', StringComparison.Ordinal);
}

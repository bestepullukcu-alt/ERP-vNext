using Diten.AuthService.Application.Features.Users.Queries;
using FluentValidation;

namespace Diten.AuthService.Application.Features.Users.Validators;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — search ≤ 100 characters; limit 1..50. A failure surfaces as the service's standard
/// validator 400 (GlobalExceptionHandler ProblemDetails, title "Validation failed"), like every validator in AuthService.
/// </summary>
public sealed class LookupUsersQueryValidator : AbstractValidator<LookupUsersQuery>
{
    public const int MaxSearchLength = 100;
    public const int MinLimit = 1;
    public const int MaxLimit = 50;

    public LookupUsersQueryValidator()
    {
        RuleFor(x => x.Search)
            .MaximumLength(MaxSearchLength).WithMessage($"Search may be at most {MaxSearchLength} characters.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(MinLimit, MaxLimit).WithMessage($"Limit must be between {MinLimit} and {MaxLimit}.");
    }
}

using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;
using FluentValidation;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Validators;

/// <summary>
/// Refuses a malformed list request with 400 (the ValidationBehavior turns these into Response.Fail(…, 400)).
/// ⚠ The `orderBy` rule is the whitelist: without it an unknown column reaches the handler — measured by sabotage in
/// GoldenCompactServerListTests.Unknown_orderBy_is_refused_400.
/// </summary>
public sealed class GetGoldenReferenceCompactListValidator : AbstractValidator<GetGoldenReferenceCompactListQuery>
{
    public GetGoldenReferenceCompactListValidator()
    {
        RuleFor(x => x.Start).GreaterThanOrEqualTo(0).When(x => x.Start.HasValue)
            .WithMessage("'start' must be 0 or greater.");
        RuleFor(x => x.Length).InclusiveBetween(1, GetGoldenReferenceCompactListQuery.MaxLength).When(x => x.Length.HasValue)
            .WithMessage($"'length' must be between 1 and {GetGoldenReferenceCompactListQuery.MaxLength}.");
        RuleFor(x => x.OrderBy).Must(orderBy => GoldenReferenceCompactListSort.TryResolve(orderBy, out _))
            .WithMessage(x => $"'orderBy' '{x.OrderBy}' is not a sortable column. Allowed: {string.Join(", ", GoldenReferenceCompactListSort.Names)}.");
        RuleFor(x => x.OrderDir).Must(GoldenReferenceCompactListSort.IsDirection)
            .WithMessage("'orderDir' must be 'asc' or 'desc'.");
        RuleForEach(x => x.Status).Must(value => GoldenReferenceCompactListSort.TryStatus(value, out _)).When(x => x.Status is not null)
            .WithMessage("'status' must be 'Active' or 'Passive'.");
        RuleFor(x => x.Search).MaximumLength(200).When(x => x.Search is not null)
            .WithMessage("'search' must be 200 characters or fewer.");
    }
}

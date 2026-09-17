using Diten.PpmService.Domain.Entities;
using FluentValidation;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class ChangePortfolioOwnerValidator : AbstractValidator<ChangePortfolioOwnerCommand>
{
    public ChangePortfolioOwnerValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Operation).IsInEnum();
        RuleFor(x => x.ExpectedAssignmentId).Null().When(x => x.Operation == PortfolioOwnerOperation.Assign);
        RuleFor(x => x.ExpectedAssignmentId).NotNull().NotEqual(Guid.Empty)
            .When(x => x.Operation == PortfolioOwnerOperation.Transfer);
    }
}

using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;
using FluentValidation;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Validators;

public sealed class GetEmployeeValidator : AbstractValidator<GetEmployeeQuery>
{
    public GetEmployeeValidator()
    {
        RuleFor(query => query.EmployeeId)
            .NotEmpty();
    }
}

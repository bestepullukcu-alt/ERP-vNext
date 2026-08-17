using Diten.Platform.Application.Features.TenantOrganization.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class UpdatePositionAssignmentCommandValidator : AbstractValidator<UpdatePositionAssignmentCommand>
{
    public UpdatePositionAssignmentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new PositionAssignmentRequestValidator<UpdatePositionAssignmentCommand>(x => x.Request));
    }
}

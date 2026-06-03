using Diten.Platform.Application.Features.TenantOrganization.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class CreatePositionAssignmentCommandValidator : AbstractValidator<CreatePositionAssignmentCommand>
{
    public CreatePositionAssignmentCommandValidator() => Include(new PositionAssignmentRequestValidator<CreatePositionAssignmentCommand>(x => x.Request));
}

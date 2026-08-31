using FluentValidation;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(64); RuleFor(x => x.Name).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(2000); RuleFor(x => x.ParentType).IsInEnum(); RuleFor(x => x.ParentId).NotEmpty(); RuleFor(x => x.VisibilityPolicyKey).Null(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}

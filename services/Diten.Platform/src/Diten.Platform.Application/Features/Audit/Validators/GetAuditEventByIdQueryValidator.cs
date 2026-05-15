using Diten.Platform.Application.Features.Audit.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Audit.Validators;

public sealed class GetAuditEventByIdQueryValidator : AbstractValidator<GetAuditEventByIdQuery>
{
    public GetAuditEventByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Audit event id is required.");
    }
}

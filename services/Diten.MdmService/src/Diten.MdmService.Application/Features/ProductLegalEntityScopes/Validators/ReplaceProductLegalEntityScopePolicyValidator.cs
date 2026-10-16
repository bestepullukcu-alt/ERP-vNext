using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Validators;

public sealed class ReplaceProductLegalEntityScopePolicyValidator
    : AbstractValidator<ReplaceProductLegalEntityScopePolicyCommand>
{
    public ReplaceProductLegalEntityScopePolicyValidator()
    {
        RuleFor(x => x.GlobalProductId).NotEmpty();
        RuleFor(x => x.CommandId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.Mode).Must(Enum.IsDefined);
            RuleFor(x => x.Request.LegalEntityIds)
                .NotNull()
                .Must(ids => ids.Count <= ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot)
                .Must(ids => ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
                .Must((command, ids) => IsModeConsistent(command.Request.Mode, ids));
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || fields.Count == 0)
                .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
        });
    }

    private static bool IsModeConsistent(ProductLegalEntityScopeMode mode, IReadOnlyList<Guid> ids) =>
        mode == ProductLegalEntityScopeMode.GroupWide ? ids.Count == 0 : ids.Count > 0;
}

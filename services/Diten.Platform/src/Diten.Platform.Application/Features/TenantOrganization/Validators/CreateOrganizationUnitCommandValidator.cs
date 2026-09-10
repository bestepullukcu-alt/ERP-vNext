using Diten.Platform.Application.Features.TenantOrganization.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

public sealed class CreateOrganizationUnitCommandValidator : AbstractValidator<CreateOrganizationUnitCommand>
{
    public CreateOrganizationUnitCommandValidator()
    {
        Include(new OrganizationUnitRequestValidator<CreateOrganizationUnitCommand>(x => x.Request));

        /*
         * MOD-0288-FU02 — the only second-parent rule that can be settled WITHOUT reading anything: an empty
         * GUID is not "no parent", it is a malformed one. Everything else about the administrative line —
         * existence, tenant, legal entity, cycles — needs the repository and therefore lives in the handler.
         *
         * ⚠ AND NOTE WHAT IS DELIBERATELY ABSENT: there is no rule forbidding the administrative parent from
         * equalling the functional one. An administrator may set both lines to the same unit (§12); what is
         * forbidden is the SYSTEM manufacturing that pair, and no validator can tell the two apart — only the
         * absence of a copying code path can.
         */
        RuleFor(x => x.Request.AdministrativeParentOrganizationUnitId)
            .NotEqual(Guid.Empty)
            .When(x => x.Request.AdministrativeParentOrganizationUnitId.HasValue);
    }
}

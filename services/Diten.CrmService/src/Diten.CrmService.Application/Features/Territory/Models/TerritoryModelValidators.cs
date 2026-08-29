using System;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Territory.Models;

public sealed class CreateTerritoryModelCommandValidator : AbstractValidator<CreateTerritoryModelCommand>
{
    public CreateTerritoryModelCommandValidator()
    {
        RuleFor(x => x.ModelCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EffectiveTo)
            .Must((cmd, to) => to is null || to.Value >= cmd.EffectiveFrom)
            .WithMessage("EffectiveTo must be on or after EffectiveFrom.");

        this.AddBusinessScopeRules();
    }
}

public sealed class UpdateTerritoryModelCommandValidator : AbstractValidator<UpdateTerritoryModelCommand>
{
    public UpdateTerritoryModelCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EffectiveTo)
            .Must((cmd, to) => to is null || to.Value >= cmd.EffectiveFrom)
            .WithMessage("EffectiveTo must be on or after EffectiveFrom.");

        this.AddBusinessScopeRules();
    }
}

/// <summary>FU02A shared structural rules for the Business Unit scopes: scopeType must be <c>business-unit</c> and
/// scopeCode is required. Reference-value existence + duplicate collapsing happen in the handler resolver (async).</summary>
internal static class TerritoryBusinessScopeRules
{
    public static void AddBusinessScopeRules(this AbstractValidator<CreateTerritoryModelCommand> v)
        => v.RuleForEach(x => x.BusinessScopes).ChildRules(Apply);

    public static void AddBusinessScopeRules(this AbstractValidator<UpdateTerritoryModelCommand> v)
        => v.RuleForEach(x => x.BusinessScopes).ChildRules(Apply);

    private static void Apply(InlineValidator<TerritoryBusinessScopeInput> scope)
    {
        scope.RuleFor(s => s.ScopeCode)
            .NotEmpty().WithMessage("BusinessScopes: scopeCode is required.");
        scope.RuleFor(s => s.ScopeType)
            .Must(t => string.Equals(t?.Trim(), TerritoryReferenceSets.BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"BusinessScopes: only scopeType '{TerritoryReferenceSets.BusinessUnitScopeType}' is supported.");
    }
}

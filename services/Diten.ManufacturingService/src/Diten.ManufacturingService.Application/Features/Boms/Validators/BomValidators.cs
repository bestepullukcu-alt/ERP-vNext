using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Application.Features.Boms.Commands;
using Diten.ManufacturingService.Application.Features.Boms.Queries;
using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Rules;
using FluentValidation;

namespace Diten.ManufacturingService.Application.Features.Boms.Validators;

/// <summary>Taslak içeriğinin ortak kuralları (pack §12). Referans varlığı (MOD-0290) handler'da, seam ile.</summary>
public sealed class BomDraftContentValidator : AbstractValidator<IBomDraftContent>
{
    public const int MaxComponents = 500;
    public const int MaxSteps = 200;

    public BomDraftContentValidator()
    {
        RuleFor(x => x.Description).MaximumLength(200).WithMessage("description must be at most 200 characters.");

        RuleFor(x => x.Components)
            .NotNull().WithMessage("components is required.")
            .Must(c => c is { Count: >= 1 and <= MaxComponents }).WithMessage($"components must contain 1 to {MaxComponents} lines.")
            .Must(c => c is null || c.Select(l => l.Position).Distinct().Count() == c.Count).WithMessage("position must be unique within a BOM version.");

        RuleForEach(x => x.Components).ChildRules(line =>
        {
            line.RuleFor(l => l.ComponentItemId).NotEqual(Guid.Empty).WithMessage("componentItemId must be a non-empty uuid.");
            line.RuleFor(l => l.Quantity).Must(BomDecimal.IsPositive).WithMessage("quantity must be a positive decimal string.");
            line.RuleFor(l => l.UomId).NotEmpty().MaximumLength(32).WithMessage("uomId is required (max 32).");
            line.RuleFor(l => l.Position).InclusiveBetween(1, 9999).WithMessage("position must be between 1 and 9999.");
            line.RuleFor(l => l.Alternates).Must(a => a is null || (a.Count <= 10 && a.All(g => g != Guid.Empty)))
                .WithMessage("alternates must hold at most 10 non-empty uuids.");
        }).When(x => x.Components is not null);

        RuleFor(x => x.Routing!.Steps)
            .Must(s => s is null || (s.Count <= MaxSteps && s.Select(step => step.StepNo).Distinct().Count() == s.Count))
            .WithMessage($"routing.steps must hold at most {MaxSteps} steps with unique stepNo.")
            .When(x => x.Routing is not null);

        RuleForEach(x => x.Routing!.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.StepNo).InclusiveBetween(1, 9999).WithMessage("stepNo must be between 1 and 9999.");
            step.RuleFor(s => s.Operation).NotEmpty().MaximumLength(120).WithMessage("operation is required (max 120).");
            step.RuleFor(s => s.WorkCenter).MaximumLength(32).WithMessage("workCenter must be at most 32 characters.");
        }).When(x => x.Routing?.Steps is not null);
    }
}

public sealed class CreateBomDraftValidator : AbstractValidator<CreateBomDraftCommand>
{
    public CreateBomDraftValidator()
    {
        RuleFor(x => x.Body).NotNull();
        RuleFor(x => x.Body.ItemId).NotEqual(Guid.Empty).WithMessage("itemId must be a non-empty uuid.").When(x => x.Body is not null);
        RuleFor(x => x.Body).SetValidator(new BomDraftContentValidator()).When(x => x.Body is not null);
    }
}

public sealed class UpdateBomDraftValidator : AbstractValidator<UpdateBomDraftCommand>
{
    public UpdateBomDraftValidator()
    {
        RuleFor(x => x.Body).NotNull();
        RuleFor(x => x.Body.RowVersion).GreaterThanOrEqualTo(0).WithMessage("rowVersion is required.");
        RuleFor(x => x.Body).SetValidator(new BomDraftContentValidator()).When(x => x.Body is not null);
    }
}

public sealed class ReleaseBomVersionValidator : AbstractValidator<ReleaseBomVersionCommand>
{
    public ReleaseBomVersionValidator()
    {
        RuleFor(x => x.Body).NotNull();
        RuleFor(x => x.Body.ChangeControlRef).NotEmpty().MaximumLength(FormatOnlyChangeControlGate.MaxLength)
            .WithMessage($"changeControlRef is required (max {FormatOnlyChangeControlGate.MaxLength}).");
        RuleFor(x => x.Body.RowVersion).GreaterThanOrEqualTo(0).WithMessage("rowVersion is required.");
    }
}

public sealed class ExplodeBomValidator : AbstractValidator<ExplodeBomQuery>
{
    public ExplodeBomValidator()
    {
        RuleFor(x => x.Body).NotNull();
        RuleFor(x => x.Body.ItemId).NotEqual(Guid.Empty).WithMessage("itemId must be a non-empty uuid.");
        RuleFor(x => x.Body.Quantity).Must(BomDecimal.IsPositive).WithMessage("quantity must be a positive decimal string.");
    }
}

public sealed class GetBomListValidator : AbstractValidator<GetBomListQuery>
{
    public GetBomListValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).Must(s => s is null || Enum.GetNames<BomStatus>().Contains(s, StringComparer.Ordinal))
            .WithMessage("status must be Draft, Effective or Superseded.");
    }
}

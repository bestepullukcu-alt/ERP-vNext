using FluentValidation;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Commands;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Validators;
// §0.5 stage 4: an invalid body is 400 even under a used key, so this runs before the handler's receipt lookup.
public sealed class SubmitSupplierEvaluationValidator : AbstractValidator<SubmitSupplierEvaluationCommand>
{ public SubmitSupplierEvaluationValidator() { RuleFor(x => x.Body).Must(SupplierPerformanceWire.SubmitValid); } }

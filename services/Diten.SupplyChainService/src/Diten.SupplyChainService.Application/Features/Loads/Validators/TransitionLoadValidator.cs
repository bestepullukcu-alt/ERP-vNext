using FluentValidation;
using Diten.SupplyChainService.Application.Features.Loads.Commands;
namespace Diten.SupplyChainService.Application.Features.Loads.Validators;
public sealed class TransitionLoadValidator : AbstractValidator<TransitionLoadCommand> { public TransitionLoadValidator() { RuleFor(x=>x.Body).Must(LoadWire.TransitionValid); } }

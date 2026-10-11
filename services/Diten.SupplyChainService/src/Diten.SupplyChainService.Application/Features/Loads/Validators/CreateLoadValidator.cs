using FluentValidation;
using Diten.SupplyChainService.Application.Features.Loads.Commands;
namespace Diten.SupplyChainService.Application.Features.Loads.Validators;
public sealed class CreateLoadValidator : AbstractValidator<CreateLoadCommand> { public CreateLoadValidator() { RuleFor(x=>x.Body).Must(LoadWire.CreateValid); } }

using FluentValidation;
using Diten.SupplyChainService.Application.Features.Returns.Commands;
namespace Diten.SupplyChainService.Application.Features.Returns.Validators;
public sealed class TransitionReturnValidator:AbstractValidator<TransitionReturnCommand>
{ public TransitionReturnValidator(){RuleFor(x=>x.Body).Must(ReturnWire.TransitionValid);} }

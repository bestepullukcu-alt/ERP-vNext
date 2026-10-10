using FluentValidation;
using Diten.SupplyChainService.Application.Features.Returns.Commands;
namespace Diten.SupplyChainService.Application.Features.Returns.Validators;
public sealed class CreateReturnValidator:AbstractValidator<CreateReturnCommand>
{ public CreateReturnValidator(){RuleFor(x=>x.Body).Must(ReturnWire.CreateValid);} }

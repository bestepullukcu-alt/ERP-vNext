using FluentValidation;
using Diten.SupplyChainService.Domain.Features.Claims;
using Diten.SupplyChainService.Application.Features.Claims.Commands;
namespace Diten.SupplyChainService.Application.Features.Claims.Validators;
public sealed class CreateClaimValidator:AbstractValidator<CreateClaimCommand> { public CreateClaimValidator() { RuleFor(x=>x.Body).Must(ClaimWire.CreateValid); } }

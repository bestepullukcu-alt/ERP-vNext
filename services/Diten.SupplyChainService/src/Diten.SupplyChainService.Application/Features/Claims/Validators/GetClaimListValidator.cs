using FluentValidation;
using Diten.SupplyChainService.Domain.Features.Claims;
using Diten.SupplyChainService.Application.Features.Claims.Queries;
namespace Diten.SupplyChainService.Application.Features.Claims.Validators;
public sealed class GetClaimListValidator:AbstractValidator<GetClaimListQuery> { public GetClaimListValidator() { RuleFor(x=>x.Status).Must(x=>x is null||Enum.GetNames<ClaimStatus>().Contains(x)); RuleFor(x=>x.ShipmentId).Must(x=>x is null||ClaimWire.Uuid(x)); } }

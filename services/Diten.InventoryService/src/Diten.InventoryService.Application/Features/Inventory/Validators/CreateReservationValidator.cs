using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using FluentValidation;
using FluentValidation.Results;

namespace Diten.InventoryService.Application.Features.Inventory.Validators;

/// <summary>Structural rules of a reservation request, delegated to <see cref="ReservationRequestParser"/> (shared with the handler).</summary>
public sealed class CreateReservationValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationValidator()
    {
        RuleFor(x => x).Custom((command, context) =>
        {
            var (_, errors) = ReservationRequestParser.Parse(command, DateTimeOffset.UtcNow);
            foreach (var error in errors)
            {
                context.AddFailure(new ValidationFailure(string.Empty, error.Message) { ErrorCode = error.Code });
            }
        });
    }
}

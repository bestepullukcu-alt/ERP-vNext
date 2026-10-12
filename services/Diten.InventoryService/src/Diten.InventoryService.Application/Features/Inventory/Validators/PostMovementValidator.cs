using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using FluentValidation;
using FluentValidation.Results;

namespace Diten.InventoryService.Application.Features.Inventory.Validators;

/// <summary>
/// Structural rules of a movement (pack §8 / §12), delegated to <see cref="MovementRequestParser"/> so the validator
/// and the handler share one definition. Each failure carries its contract code in <c>ErrorCode</c>.
/// </summary>
public sealed class PostMovementValidator : AbstractValidator<PostMovementCommand>
{
    public PostMovementValidator()
    {
        RuleFor(x => x).Custom((command, context) =>
        {
            var (_, errors) = MovementRequestParser.Parse(command);
            foreach (var error in errors)
            {
                context.AddFailure(new ValidationFailure(string.Empty, error.Message) { ErrorCode = error.Code });
            }
        });
    }
}

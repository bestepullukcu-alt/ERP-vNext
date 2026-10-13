using Diten.AuthService.Domain.Entities;
using FluentValidation;

namespace Diten.AuthService.Application.Features.ESignReauth;

// Messages never echo the submitted value (no {PropertyValue}): the password and the ticket must not reach a response.

public sealed class ReauthenticateForESignCommandValidator : AbstractValidator<ReauthenticateForESignCommand>
{
    public ReauthenticateForESignCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(128).WithMessage("Password is too long.");
        RuleFor(x => x.Intent)
            .Must(intent => intent is not null && ESignReauthIntents.All.Contains(intent))
            .WithMessage("Intent must be one of: sign, sample, invalidate.");
        RuleFor(x => x.OpenWindowMinutes)
            .InclusiveBetween(1, ESignReauthWindow.MaxMinutes)
            .When(x => x.OpenWindowMinutes.HasValue)
            .WithMessage("openWindowMinutes must be between 1 and 60.");
        RuleFor(x => x.OpenWindowMinutes)
            .Null()
            .When(x => x.Intent != ESignReauthIntents.Sign)
            .WithMessage("A window can only be opened for the sign intent.");
    }
}

public sealed class IssueESignWindowTicketCommandValidator : AbstractValidator<IssueESignWindowTicketCommand>
{
    public IssueESignWindowTicketCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Intent)
            .Must(intent => intent is not null && ESignReauthIntents.All.Contains(intent))
            .WithMessage("Intent must be one of: sign, sample, invalidate.");
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("User name is required.")
            .MaximumLength(256).WithMessage("User name is too long.");
    }
}

public sealed class ConsumeESignTicketCommandValidator : AbstractValidator<ConsumeESignTicketCommand>
{
    public ConsumeESignTicketCommandValidator()
    {
        RuleFor(x => x.Ticket).NotEmpty().WithMessage("Ticket is required.").MaximumLength(128).WithMessage("Ticket is malformed.");
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Intent)
            .Must(intent => intent is not null && ESignReauthIntents.All.Contains(intent))
            .WithMessage("Intent must be one of: sign, sample, invalidate.");
        RuleFor(x => x.ConsumedBy).NotEmpty().MaximumLength(64);
    }
}

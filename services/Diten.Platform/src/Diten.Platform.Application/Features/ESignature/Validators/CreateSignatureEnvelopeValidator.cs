using Diten.Platform.Application.Features.ESignature.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.ESignature.Validators;

public sealed class CreateSignatureEnvelopeValidator : AbstractValidator<CreateSignatureEnvelopeCommand>
{
    public CreateSignatureEnvelopeValidator()
    {
        RuleFor(x => x.TargetTenantId).NotEmpty();
        RuleFor(x => x.Request.SubjectType).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Request.SubjectId).NotEmpty();
        RuleFor(x => x.Request.SubjectVersion).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Request.DocumentArtifactId).NotEmpty();
        RuleFor(x => x.Request.SourceArtifactHash)
            .Matches("^[a-f0-9]{64}$")
            .WithMessage("SourceArtifactHash must be a lowercase SHA-256 hex value.");
        RuleFor(x => x.Request.SourceArtifactContent)
            .NotEmpty()
            .Must(content => content.Length <= 10 * 1024 * 1024)
            .WithMessage("SourceArtifactContent cannot exceed 10 MB.");
        RuleFor(x => x.Request.CorrelationId).NotEmpty();
        RuleFor(x => x.Request.Participants).NotEmpty().Must(x => x.Count <= 50);
        RuleForEach(x => x.Request.Participants).ChildRules(participant =>
        {
            participant.RuleFor(x => x.SignerUserId).NotEmpty();
            participant.RuleFor(x => x.SignerEmail).NotEmpty().EmailAddress().MaximumLength(256);
            participant.RuleFor(x => x.SignerDisplayName).NotEmpty().MaximumLength(200);
            participant.RuleFor(x => x.SigningOrder).GreaterThanOrEqualTo(1);
            participant.RuleFor(x => x.Role).NotEmpty().MaximumLength(120);
        });
        RuleFor(x => x.Request.Participants)
            .Must(participants => participants.Select(x => x.SigningOrder).Distinct().Count() == participants.Count)
            .WithMessage("Participant signing order values must be unique.");
    }
}

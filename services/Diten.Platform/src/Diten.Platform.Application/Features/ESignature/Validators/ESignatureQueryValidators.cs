using Diten.Platform.Application.Features.ESignature.Commands;
using Diten.Platform.Application.Features.ESignature.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.ESignature.Validators;

public sealed class GetSignatureEnvelopeListValidator : AbstractValidator<GetSignatureEnvelopeListQuery>
{
    public GetSignatureEnvelopeListValidator()
    {
        RuleFor(x => x.TargetTenantId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetSignatureEnvelopeByIdValidator : AbstractValidator<GetSignatureEnvelopeByIdQuery>
{
    public GetSignatureEnvelopeByIdValidator()
    {
        RuleFor(x => x.TargetTenantId).NotEmpty();
        RuleFor(x => x.EnvelopeId).NotEmpty();
    }
}

public sealed class GetSignatureArtifactValidator : AbstractValidator<GetSignatureArtifactQuery>
{
    public GetSignatureArtifactValidator()
    {
        RuleFor(x => x.TargetTenantId).NotEmpty();
        RuleFor(x => x.ArtifactId).NotEmpty();
    }
}

public sealed class GenerateSignatureAuditExportValidator
    : AbstractValidator<GenerateSignatureAuditExportCommand>
{
    public GenerateSignatureAuditExportValidator()
    {
        RuleFor(x => x.TargetTenantId).NotEmpty();
        RuleFor(x => x.EnvelopeId).NotEmpty();
        RuleFor(x => x.CorrelationId).NotEmpty();
    }
}

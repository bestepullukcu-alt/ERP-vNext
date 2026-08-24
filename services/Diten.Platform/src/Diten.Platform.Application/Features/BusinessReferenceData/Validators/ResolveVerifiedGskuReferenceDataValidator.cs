using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

// Semantic validation remains in the handler so every invalid shape receives the locked 409 contract.
public sealed class ResolveVerifiedGskuReferenceDataValidator
    : AbstractValidator<ResolveVerifiedGskuReferenceDataQuery>
{
}

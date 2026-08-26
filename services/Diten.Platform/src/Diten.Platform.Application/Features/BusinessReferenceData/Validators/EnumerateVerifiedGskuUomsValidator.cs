using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

// The query is intentionally bodyless and tenant-free; transport rejects all input before dispatch.
public sealed class EnumerateVerifiedGskuUomsValidator
    : AbstractValidator<EnumerateVerifiedGskuUomsQuery>
{
}

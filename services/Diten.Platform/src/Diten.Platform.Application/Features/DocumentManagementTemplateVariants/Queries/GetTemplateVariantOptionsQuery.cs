using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;

public sealed record GetTemplateVariantOptionsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TemplateVariantOptionModel>>>;

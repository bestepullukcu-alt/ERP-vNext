using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;

public sealed record GetTemplateVariantCompareQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TemplateVariantCompareModel>>;

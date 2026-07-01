using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;

public sealed record GetTemplateMasterVariantsQuery(Guid TemplateMasterId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<TemplateVariantListItemModel>>>;

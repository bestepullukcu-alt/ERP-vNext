using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;

public sealed record GetTemplateVariantByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TemplateVariantDetailModel>>;

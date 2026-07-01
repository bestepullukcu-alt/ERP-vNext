using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;

public sealed record GetTemplateMasterListQuery(TemplateMasterListFilter Filter, string CorrelationId)
    : IRequest<Response<IReadOnlyList<TemplateMasterListItemModel>>>;

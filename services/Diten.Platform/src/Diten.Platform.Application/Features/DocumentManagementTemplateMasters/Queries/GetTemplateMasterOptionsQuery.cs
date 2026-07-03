using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;

public sealed record GetTemplateMasterOptionsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TemplateMasterOptionModel>>>;

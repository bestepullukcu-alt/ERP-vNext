using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

public sealed record GetWorkCategoryByIdQuery(Guid Id, string CorrelationId) : IRequest<Response<WorkCategoryDto>>;

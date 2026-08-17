using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleAssignments.Queries;

public sealed record GetModuleAssignmentOverviewQuery(string ModuleCode, string CorrelationId)
    : IRequest<Response<ModuleAssignmentOverviewDto>>;

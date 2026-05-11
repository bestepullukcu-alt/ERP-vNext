using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleAssignments.Queries;

public sealed record GetModulePlanAssignmentsQuery(string ModuleCode, ModulePlanAssignmentFilterRequest Filter)
    : IRequest<Response<ModuleAssignmentPageDto<ModulePlanAssignmentRowDto>>>;

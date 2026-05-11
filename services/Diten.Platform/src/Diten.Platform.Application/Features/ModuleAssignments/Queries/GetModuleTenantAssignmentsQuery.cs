using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleAssignments.Queries;

public sealed record GetModuleTenantAssignmentsQuery(string ModuleCode, ModuleTenantAssignmentFilterRequest Filter)
    : IRequest<Response<ModuleAssignmentPageDto<ModuleTenantAssignmentRowDto>>>;

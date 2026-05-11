using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleAssignments.Queries;

public sealed record GetModuleTenantAssignmentDetailQuery(string ModuleCode, string TenantCode, string CorrelationId)
    : IRequest<Response<ModuleTenantAssignmentDetailDto>>;

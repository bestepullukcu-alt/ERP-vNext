using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetProjectAuditTrailQuery : IRequest<Response<IReadOnlyList<EnterpriseStrategyAuditEventDto>>>
{
    public string ProjectId { get; set; } = string.Empty;
}

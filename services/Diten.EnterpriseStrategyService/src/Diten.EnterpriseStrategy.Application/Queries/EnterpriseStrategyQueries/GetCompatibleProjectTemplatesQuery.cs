using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetCompatibleProjectTemplatesQuery : IRequest<Response<IReadOnlyList<ProjectCreationTemplateDto>>>
{
    public string ParentType { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
}

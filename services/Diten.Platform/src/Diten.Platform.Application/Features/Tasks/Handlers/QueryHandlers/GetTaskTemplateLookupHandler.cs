using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// Templates a recurrence rule can be built from (BL-052).
///
/// <para>The repository has been able to list active templates all along; nothing exposed it, so the rule screen
/// had a template picker with no source — a control that could never be filled. That is the same shape as a
/// payload nobody reads, and it is why this ships in the same slice as the screen rather than after it.</para>
/// </summary>
public sealed class GetTaskTemplateLookupHandler
    : IRequestHandler<GetTaskTemplateLookupQuery, Response<IReadOnlyList<TaskTemplateLookupDto>>>
{
    private readonly ITaskTemplateRepository _templates;

    public GetTaskTemplateLookupHandler(ITaskTemplateRepository templates) => _templates = templates;

    public async Task<Response<IReadOnlyList<TaskTemplateLookupDto>>> Handle(
        GetTaskTemplateLookupQuery request, CancellationToken ct)
    {
        // Active only, and sorted by the repository. Binding a rule to a retired template would go on generating
        // work from a shape nobody maintains any more, which nothing downstream could explain.
        IReadOnlyList<TaskTemplateLookupDto> result = (await _templates.ListActiveAsync(ct))
            .Select(template => new TaskTemplateLookupDto(template.Id, template.Name))
            .ToList();

        return Response<IReadOnlyList<TaskTemplateLookupDto>>.Success(result, correlationId: request.CorrelationId);
    }
}

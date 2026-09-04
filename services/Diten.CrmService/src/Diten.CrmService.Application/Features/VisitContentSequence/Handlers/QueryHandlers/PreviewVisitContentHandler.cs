using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitContentSequence.Queries;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitContentSequence.Handlers.QueryHandlers;

/// <summary>
/// The preview handler. It injects the <see cref="VisitContentSequenceResolver"/> seam and NOTHING that could persist —
/// no repository, no unit of work — so the endpoint could not write even by mistake (AC-EP-1). A missing subject id is a
/// controlled 400; every content / journey / capacity gap is a 200 whose <c>Status</c> + <c>ReasonCodes</c> carry the
/// coded outcome (AC-SEQ-3 / AC-SPLIT-2 / V5).
/// </summary>
public sealed class PreviewVisitContentHandler
    : IRequestHandler<PreviewVisitContentQuery, Response<VisitContentSequenceResult>>
{
    private readonly VisitContentSequenceResolver _resolver;

    public PreviewVisitContentHandler(VisitContentSequenceResolver resolver) => _resolver = resolver;

    public async Task<Response<VisitContentSequenceResult>> Handle(
        PreviewVisitContentQuery request, CancellationToken cancellationToken)
    {
        var errors = Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<VisitContentSequenceResult>.Fail(errors, 400);
        }

        var result = await _resolver.ResolveAsync(request.Request, cancellationToken);
        return Response<VisitContentSequenceResult>.Success(result);
    }

    /// <summary>Structural validation of the envelope only. A doctor (subject) is required; a segment or a strategy
    /// template that fails to resolve is NOT a 400 — it is a coded <c>no-strategy</c> result inside a 200.</summary>
    private static IReadOnlyList<string> Validate(VisitContentSequenceRequest? request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("A visit content sequence request is required.");
            return errors;
        }

        if (request.SubjectId == Guid.Empty)
        {
            errors.Add("subjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SubjectType))
        {
            errors.Add("subjectType is required.");
        }

        if (request.SegmentId is null && request.StrategyTemplateId is null)
        {
            errors.Add("Either segmentId or strategyTemplateId is required to resolve a play.");
        }

        return errors;
    }
}

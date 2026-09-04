using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitContentSequence.Queries;

/// <summary>
/// MOD-0155 FU04 — the read-only Visit Content Sequence preview (D-SURFACE = E). A QUERY in every sense: it runs the
/// in-process <see cref="VisitContentSequenceResolver"/> over the supplied context and returns the resolved next
/// content + duration, <b>persisting NOTHING</b> — no PlannedVisit write, no Mongo write, no side effect. The endpoint
/// is a thin wrapper; the FU05 packing engine calls the very same resolver in-process, so there is exactly one logic
/// path (AC-EP-2).
/// <para>A malformed request (no subject id) is a 400; every content / journey / capacity gap is a coded
/// <see cref="VisitContentSequenceResult.Status"/> inside a 200 (a coded gap is data, not an HTTP error).</para>
/// </summary>
public sealed record PreviewVisitContentQuery(VisitContentSequenceRequest Request)
    : IRequest<Response<VisitContentSequenceResult>>;

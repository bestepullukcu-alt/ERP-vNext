using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Commands;

/// <summary>Closes a segment. This is the ONLY removal there is: there is no DELETE route anywhere in this FU, because
/// a deleted segment would take every past explanation of "why was this person selected?" with it.</summary>
public sealed record ArchiveSegmentCommand(Guid SegmentId, int? ExpectedVersion) : IRequest<Response<bool>>;

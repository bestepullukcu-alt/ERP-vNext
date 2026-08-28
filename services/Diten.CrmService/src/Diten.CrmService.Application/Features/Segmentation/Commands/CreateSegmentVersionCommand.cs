using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Commands;

/// <summary>
/// Clones an existing version into a new DRAFT: business version + 1, same lineage, and a criteria tree cloned with
/// brand-new NodeIds whose parent references are remapped onto the clone own ids. Editing a live rule in place is what
/// this exists to prevent — it would silently invalidate every explanation ever given for the old version.
/// <para>The predecessor is superseded only when the new version is ACTIVATED, and it stays resolvable afterwards.</para>
/// </summary>
public sealed record CreateSegmentVersionCommand(Guid SegmentId) : IRequest<Response<Guid>>;

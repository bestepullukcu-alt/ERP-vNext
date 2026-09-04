using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Commands;

/// <summary>
/// Archives a plan (any non-archived status → archived). Terminal: there is no unarchive endpoint (§12.2). An archived
/// row is hidden from the default list (visible only with <c>includeArchived=true</c>) and accepts no further mutation.
/// </summary>
public sealed record ArchivePlannedVisitCommand(
    Guid PlannedVisitId,
    int? ExpectedVersion) : IRequest<Response<bool>>;

using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Commands;

/// <summary>
/// Closes a play. Archiving is the ONLY way to remove one: there is no delete endpoint anywhere, because a deleted
/// template would take every past explanation of "why did we run this play?" with it.
/// </summary>
public sealed record ArchiveStrategyTemplateCommand(
    Guid TemplateId,
    int? ExpectedVersion) : IRequest<Response<bool>>;

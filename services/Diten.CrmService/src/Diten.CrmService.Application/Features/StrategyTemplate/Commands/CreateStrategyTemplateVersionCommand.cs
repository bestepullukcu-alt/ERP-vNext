using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Commands;

/// <summary>
/// Clones an active template into a NEW draft version: same lineage, TemplateVersion + 1, and FRESH child ids for every
/// binding, line and allocation. This is how a frozen play is changed — the previous version stays readable so a past
/// play can still be explained, and it is superseded only when the new version goes live.
/// </summary>
public sealed record CreateStrategyTemplateVersionCommand(Guid TemplateId) : IRequest<Response<Guid>>;

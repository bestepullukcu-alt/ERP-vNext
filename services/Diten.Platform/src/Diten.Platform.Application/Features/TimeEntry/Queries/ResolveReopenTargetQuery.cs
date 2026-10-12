using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>F15 — which revision a reopen of (person, week) will touch: the open revision if there is one, else the
/// latest; null when the week has none (the reopen then creates it). Read only.</summary>
public sealed record ResolveReopenTargetQuery(Guid UserId, string? WeekKey, string CorrelationId) : IRequest<Response<Guid?>>;

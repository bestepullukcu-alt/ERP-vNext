using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>MOD-0280-FU01 D10 — the tenant's categories (active and inactive; the screen filters).</summary>
public sealed record GetWorkCategoryListQuery(bool ActiveOnly, string CorrelationId) : IRequest<Response<IReadOnlyList<WorkCategoryDto>>>;

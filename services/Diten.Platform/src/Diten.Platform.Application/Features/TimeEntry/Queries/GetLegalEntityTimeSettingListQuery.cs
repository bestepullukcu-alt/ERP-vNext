using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Queries;

/// <summary>MOD-0280-FU01 D12 — the stored switch rows. A legal entity with no row is OFF.</summary>
public sealed record GetLegalEntityTimeSettingListQuery(string CorrelationId) : IRequest<Response<IReadOnlyList<LegalEntityTimeSettingDto>>>;

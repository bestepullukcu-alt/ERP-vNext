using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Queries;

public sealed record GetPayBenchmarkingReadinessListQuery : IRequest<Response<IReadOnlyList<PayBenchmarkingReadinessListItemDto>>>;

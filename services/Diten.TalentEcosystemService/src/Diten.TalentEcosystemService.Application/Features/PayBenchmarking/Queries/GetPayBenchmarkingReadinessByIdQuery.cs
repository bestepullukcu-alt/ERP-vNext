using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Queries;

public sealed record GetPayBenchmarkingReadinessByIdQuery(Guid Id) : IRequest<Response<PayBenchmarkingReadinessDto>>;

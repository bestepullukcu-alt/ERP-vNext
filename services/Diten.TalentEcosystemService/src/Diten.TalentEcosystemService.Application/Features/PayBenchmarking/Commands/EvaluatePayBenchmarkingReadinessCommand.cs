using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Commands;

public sealed record EvaluatePayBenchmarkingReadinessCommand(Guid Id) : IRequest<Response<PayBenchmarkingReadinessDto>>;

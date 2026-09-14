using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Commands;

public sealed record CreatePayBenchmarkingReadinessCommand(PayBenchmarkingReadinessCreateRequest Request) : IRequest<Response<Guid>>;

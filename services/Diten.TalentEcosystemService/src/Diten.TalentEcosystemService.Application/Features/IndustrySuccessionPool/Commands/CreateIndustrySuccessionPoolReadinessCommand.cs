using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Commands;

public sealed record CreateIndustrySuccessionPoolReadinessCommand(IndustrySuccessionPoolReadinessCreateRequest Request) : IRequest<Response<Guid>>;

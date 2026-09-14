using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Queries;

public sealed record GetProfessionalReputationLedgerReadinessListQuery : IRequest<Response<IReadOnlyList<ProfessionalReputationLedgerReadinessListItemDto>>>;

using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Queries;

public sealed record GetVerifiedParticipantAccessAuditMetadataQuery(Guid Id) : IRequest<Response<VerifiedParticipantAuditMetadataDto>>;

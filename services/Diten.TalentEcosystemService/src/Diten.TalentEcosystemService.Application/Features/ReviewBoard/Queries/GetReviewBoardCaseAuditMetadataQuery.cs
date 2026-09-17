using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Queries;

public sealed record GetReviewBoardCaseAuditMetadataQuery(Guid Id) : IRequest<Response<ReviewBoardAuditMetadataDto>>;

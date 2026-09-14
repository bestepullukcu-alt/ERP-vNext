using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Queries;

public sealed record GetReviewBoardCaseListQuery : IRequest<Response<IReadOnlyList<ReviewBoardCaseListItemDto>>>;

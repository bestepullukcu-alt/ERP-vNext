using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;

public sealed record ReviewReviewBoardCaseCommand(Guid Id, ReviewBoardDecisionRequest Request)
    : IRequest<Response<ReviewBoardCaseDto>>;

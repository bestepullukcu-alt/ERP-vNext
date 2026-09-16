using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;

public sealed record EvaluateReviewBoardCaseCommand(Guid Id, EvaluateReviewBoardCaseRequest Request)
    : IRequest<Response<ReviewBoardEvaluationDto>>;

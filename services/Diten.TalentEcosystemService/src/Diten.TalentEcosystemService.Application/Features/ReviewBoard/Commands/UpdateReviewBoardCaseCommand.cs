using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;

public sealed record UpdateReviewBoardCaseCommand(Guid Id, ReviewBoardCaseRequest Request) : IRequest<Response<ReviewBoardCaseDto>>;

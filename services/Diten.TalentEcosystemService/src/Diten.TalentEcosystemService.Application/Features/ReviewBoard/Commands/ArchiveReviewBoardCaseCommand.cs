using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;

public sealed record ArchiveReviewBoardCaseCommand(Guid Id) : IRequest<Response<NoContent>>;

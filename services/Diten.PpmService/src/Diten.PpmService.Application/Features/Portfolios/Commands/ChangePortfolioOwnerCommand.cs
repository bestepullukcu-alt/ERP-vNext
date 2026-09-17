using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed record ChangePortfolioOwnerCommand(Guid Id, Guid TargetUserId, string Reason,
    PortfolioOwnerOperation Operation, Guid? ExpectedAssignmentId, int ExpectedVersion, Guid RequestId)
    : IRequest<Response<PortfolioOwnerReceipt>>;

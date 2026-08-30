using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Programs;

public sealed record CreateProgramCommand(string Code, string Name, string? Description, Guid? PortfolioId, string? VisibilityPolicyKey) : IRequest<Response<ProgramDto>>;

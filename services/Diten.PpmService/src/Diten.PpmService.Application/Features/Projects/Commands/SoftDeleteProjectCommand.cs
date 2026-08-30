using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Projects;

public sealed record SoftDeleteProjectCommand(Guid Id, int ExpectedVersion) : IRequest<Response<NoContent>>;

using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Commands;

public sealed record UpdateHrisMappingProfileCommand(Guid SourceProfileId, HrisMappingProfileRequest Request) : IRequest<Response<NoContent>>;

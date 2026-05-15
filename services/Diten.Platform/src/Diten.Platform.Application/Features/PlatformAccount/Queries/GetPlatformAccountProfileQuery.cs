using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAccount.Queries;

public sealed record GetPlatformAccountProfileQuery : IRequest<Response<PlatformAccountProfileDto>>;

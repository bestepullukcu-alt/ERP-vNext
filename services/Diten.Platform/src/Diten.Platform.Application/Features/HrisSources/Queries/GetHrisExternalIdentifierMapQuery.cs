using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Queries;

public sealed record GetHrisExternalIdentifierMapQuery(Guid SourceProfileId) : IRequest<Response<IReadOnlyList<HrisExternalIdentifierMapDto>>>;

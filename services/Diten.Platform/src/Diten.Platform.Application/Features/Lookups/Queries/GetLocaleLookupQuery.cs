using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Lookups;
using MediatR;

namespace Diten.Platform.Application.Features.Lookups.Queries;

public sealed record GetLocaleLookupQuery() : IRequest<Response<IReadOnlyList<LookupOptionDto>>>;

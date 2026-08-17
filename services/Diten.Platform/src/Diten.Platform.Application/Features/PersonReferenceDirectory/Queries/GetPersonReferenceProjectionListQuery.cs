using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Queries;

public sealed record GetPersonReferenceProjectionListQuery : IRequest<Response<IReadOnlyList<PersonReferenceProjectionListItemDto>>>;

using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Queries;

public sealed record GetCountryByIdQuery(Guid Id) : IRequest<Country?>;
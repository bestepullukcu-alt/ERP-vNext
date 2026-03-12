using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Queries;

public sealed record GetAllCountriesQuery() : IRequest<IEnumerable<Country>>;


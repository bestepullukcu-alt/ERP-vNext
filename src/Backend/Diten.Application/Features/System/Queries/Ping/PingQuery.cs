using Diten.Application.Common.Models;
using MediatR;

namespace Diten.Application.Features.System.Queries.Ping;

public class PingQuery : IRequest<Response<string>>
{
}

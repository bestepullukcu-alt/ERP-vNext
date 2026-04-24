using System;
using System.Threading;
using System.Threading.Tasks;
using Diten.Application.Common.Models;
using MediatR;

namespace Diten.Application.Features.System.Queries.Ping;

public class PingHandler : IRequestHandler<PingQuery, Response<string>>
{
    public Task<Response<string>> Handle(PingQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Response<string>.Ok("Pong! System is up and running."));
    }
}

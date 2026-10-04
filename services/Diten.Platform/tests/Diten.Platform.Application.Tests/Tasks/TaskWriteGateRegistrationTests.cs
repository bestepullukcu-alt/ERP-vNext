using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX2 — the request handlers as PRODUCTION wires them (<c>AddApplication</c>: the real MediatR
/// registration and the real service registrations), not as the HTTP suite's router builds them by hand. An OPTIONAL
/// dependency the container does not know is passed as null without a word — for the accept handler a null read rule
/// is a 404 on every accept — so each one must be registered.
/// </summary>
public sealed class TaskWriteGateRegistrationTests
{
    [Fact]
    public void AddApplication_wires_the_request_handlers_with_their_read_rule_request_service_and_write_gate()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<AcceptTaskItemCommand, Response<NoContent>>)
                                       && d.ImplementationType == typeof(AcceptTaskItemHandler));
        foreach (var required in new[] { typeof(ITaskReadAccessPolicy), typeof(ITaskRequestService), typeof(ITaskWriteGate) })
        {
            Assert.Contains(services, d => d.ServiceType == required);
        }

        foreach (var handler in new[] { typeof(AcceptTaskItemHandler), typeof(ReturnTaskItemHandler), typeof(CreateTaskItemHandler) })
        {
            foreach (var optional in handler.GetConstructors().Single().GetParameters().Where(p => p.HasDefaultValue))
            {
                Assert.True(services.Any(d => d.ServiceType == optional.ParameterType),
                    $"{handler.Name}: optional {optional.ParameterType.Name} {optional.Name} is not registered — it would be null in production");
            }
        }
    }
}

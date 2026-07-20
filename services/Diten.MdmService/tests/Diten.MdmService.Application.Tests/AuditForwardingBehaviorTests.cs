using Diten.MdmService.Application.Behaviors;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.LegalEntity.Commands;
using Diten.Shared.Core;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.MdmService.Application.Tests;

// MOD-0021 Faz 2 — the audit pipeline behavior forwards a completed Legal Entity command to Platform's central audit
// store (S2S). Verifies the forward IS made and the metadata/EntityId/outcome are correct, with the S2S client mocked.
public sealed class AuditForwardingBehaviorTests
{
    private sealed class CapturingForwarder : IPlatformAuditForwarder
    {
        public List<AuditForwardRequest> Requests { get; } = [];

        public Task ForwardAsync(AuditForwardRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private static AuditForwardingBehavior<TRequest, TResponse> Behavior<TRequest, TResponse>(CapturingForwarder forwarder)
        where TRequest : IRequest<TResponse>
    {
        return new AuditForwardingBehavior<TRequest, TResponse>(
            forwarder,
            NullLogger<AuditForwardingBehavior<TRequest, TResponse>>.Instance);
    }

    [Fact]
    public async Task CreateLegalEntity_forwards_audit_with_entityId_from_response()
    {
        var forwarder = new CapturingForwarder();
        var behavior = Behavior<CreateLegalEntityCommand, Response<Guid>>(forwarder);
        var newId = Guid.NewGuid();

        var response = await behavior.Handle(
            new CreateLegalEntityCommand(null!),
            () => Task.FromResult(Response<Guid>.Success(newId, 201)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var forwarded = Assert.Single(forwarder.Requests);
        Assert.Equal(nameof(CreateLegalEntityCommand), forwarded.RequestType);
        Assert.Equal((int)AuditCategory.MasterData, forwarded.Category);
        Assert.Equal((int)AuditOperation.Create, forwarded.Operation);
        Assert.Equal("LegalEntity", forwarded.EntityType);
        Assert.Equal("legal-entity", forwarded.SourceModule);
        Assert.Equal(newId, forwarded.EntityId);   // create: id comes from the handler response
        Assert.Equal(1, forwarded.Outcome);         // AuditOutcome.Succeeded
    }

    [Fact]
    public async Task ArchiveLegalEntity_forwards_deactivate_with_entityId_from_command()
    {
        var forwarder = new CapturingForwarder();
        var behavior = Behavior<ArchiveLegalEntityCommand, Response<NoContent>>(forwarder);
        var legalEntityId = Guid.NewGuid();

        await behavior.Handle(
            new ArchiveLegalEntityCommand(legalEntityId),
            () => Task.FromResult(Response<NoContent>.SuccessWithoutData()),
            CancellationToken.None);

        var forwarded = Assert.Single(forwarder.Requests);
        Assert.Equal(nameof(ArchiveLegalEntityCommand), forwarded.RequestType);
        Assert.Equal((int)AuditOperation.Deactivate, forwarded.Operation);   // Archive maps to Deactivate
        Assert.Equal(legalEntityId, forwarded.EntityId);                     // lifecycle: id comes from the command
    }

    [Fact]
    public async Task FailedCommand_forwards_failed_outcome()
    {
        var forwarder = new CapturingForwarder();
        var behavior = Behavior<UpdateLegalEntityCommand, Response<NoContent>>(forwarder);

        await behavior.Handle(
            new UpdateLegalEntityCommand(Guid.NewGuid(), null!),
            () => Task.FromResult(Response<NoContent>.Fail("boom", 409)),
            CancellationToken.None);

        var forwarded = Assert.Single(forwarder.Requests);
        Assert.Equal(2, forwarded.Outcome); // AuditOutcome.Failed
    }

    [Fact]
    public async Task NonAuditableCommand_does_not_forward()
    {
        var forwarder = new CapturingForwarder();
        var behavior = Behavior<PlainCommand, Response<NoContent>>(forwarder);

        await behavior.Handle(
            new PlainCommand(),
            () => Task.FromResult(Response<NoContent>.SuccessWithoutData()),
            CancellationToken.None);

        Assert.Empty(forwarder.Requests);
    }

    [Fact]
    public async Task ForwarderThrows_does_not_break_the_command()
    {
        var behavior = new AuditForwardingBehavior<CreateLegalEntityCommand, Response<Guid>>(
            new ThrowingForwarder(),
            NullLogger<AuditForwardingBehavior<CreateLegalEntityCommand, Response<Guid>>>.Instance);

        var response = await behavior.Handle(
            new CreateLegalEntityCommand(null!),
            () => Task.FromResult(Response<Guid>.Success(Guid.NewGuid())),
            CancellationToken.None);

        Assert.True(response.IsSuccessful); // best-effort: forwarding failure is swallowed
    }

    private sealed record PlainCommand : IRequest<Response<NoContent>>;

    private sealed class ThrowingForwarder : IPlatformAuditForwarder
    {
        public Task ForwardAsync(AuditForwardRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("Platform unreachable");
    }
}

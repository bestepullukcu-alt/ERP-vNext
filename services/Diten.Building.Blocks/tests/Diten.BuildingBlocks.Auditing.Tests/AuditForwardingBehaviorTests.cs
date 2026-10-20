using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.BuildingBlocks.Auditing.Tests;

/// <summary>AUD-001 K4 (WP-VW-W6 F8-a) — one record per execution of a marked command: success, refusal, exception.</summary>
public sealed class AuditForwardingBehaviorTests
{
    private readonly RecordingForwarder _forwarder = new();
    private readonly AuditForwardingCounters _counters = new();

    private AuditForwardingBehavior<TRequest, Envelope<ThingDto>> Behavior<TRequest>(IPlatformAuditForwarder? forwarder = null)
        where TRequest : notnull
        => new(forwarder ?? _forwarder, _counters, new ListLogger<AuditForwardingBehavior<TRequest, Envelope<ThingDto>>>());

    [Fact]
    public async Task A_succeeded_command_is_forwarded_once_with_its_descriptor_and_details()
    {
        var id = Guid.NewGuid();
        var command = new RenameThingCommand(id, "new name");

        var response = await Behavior<RenameThingCommand>().Handle(command, () => Task.FromResult(Envelope<ThingDto>.Ok(new ThingDto(id, 4))), default);

        Assert.True(response.IsSuccessful);
        var record = Assert.Single(_forwarder.Requests);
        Assert.Equal(nameof(RenameThingCommand), record.RequestType);
        Assert.Equal(AuditCategory.Crm, record.Category);
        Assert.Equal(AuditOperation.Update, record.Operation);
        Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
        Assert.Equal("Thing", record.EntityType);
        Assert.Equal(id, record.EntityId);
        Assert.Equal("MOD-0155", record.SourceModule);
        Assert.Equal(4, record.Metadata![AuditDetails.ObjectVersionKey]);
        Assert.Equal(new[] { "name" }, Assert.IsType<string[]>(record.Metadata[AuditDetails.ChangedFieldsKey]));
    }

    [Fact]
    public async Task A_create_takes_its_entity_id_from_the_response()
    {
        var id = Guid.NewGuid();

        await Behavior<CreateThingCommand>().Handle(new CreateThingCommand("x"), () => Task.FromResult(Envelope<ThingDto>.Ok(new ThingDto(id, 1))), default);

        Assert.Equal(id, Assert.Single(_forwarder.Requests).EntityId);
    }

    [Fact]
    public async Task A_refused_command_is_recorded_as_failed_with_its_code_and_status_but_never_its_message()
    {
        var response = await Behavior<RenameThingCommand>().Handle(
            new RenameThingCommand(Guid.NewGuid(), "n"),
            () => Task.FromResult(Envelope<ThingDto>.Fail(409, "The thing changed since it was loaded. Reload.", "thing_version_conflict")),
            default);

        Assert.False(response.IsSuccessful);
        var record = Assert.Single(_forwarder.Requests);
        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal("thing_version_conflict", record.Metadata![AuditDetails.RefusalCodeKey]);
        Assert.Equal(409, record.Metadata[AuditDetails.StatusCodeKey]);
        Assert.DoesNotContain(record.Metadata.Values, value => value is string text && text.Contains("Reload", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task A_refusal_for_lack_of_rights_is_recorded_as_denied(int status)
    {
        await Behavior<RenameThingCommand>().Handle(
            new RenameThingCommand(Guid.NewGuid(), "n"), () => Task.FromResult(Envelope<ThingDto>.Fail(status, "forbidden")), default);

        Assert.Equal(AuditOutcome.Denied, Assert.Single(_forwarder.Requests).Outcome);
    }

    [Fact]
    public async Task A_command_that_throws_is_recorded_as_failed_and_the_exception_is_rethrown_unchanged()
    {
        var thrown = new InvalidOperationException("store down");

        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() => Behavior<RenameThingCommand>().Handle(
            new RenameThingCommand(Guid.NewGuid(), "n"), () => throw thrown, default));

        Assert.Same(thrown, caught);
        var record = Assert.Single(_forwarder.Requests);
        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal(nameof(InvalidOperationException), record.Metadata![AuditDetails.ErrorTypeKey]);
        Assert.DoesNotContain(record.Metadata.Values, value => value is string text && text.Contains("store down", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unmarked_command_is_not_forwarded()
    {
        await Behavior<UnmarkedCommand>().Handle(new UnmarkedCommand("x"), () => Task.FromResult(Envelope<ThingDto>.Ok(new ThingDto(Guid.NewGuid(), 1))), default);

        Assert.Empty(_forwarder.Requests);
    }

    [Fact]
    public async Task A_secret_named_key_never_carries_its_value()
    {
        await Behavior<RenameThingCommand>().Handle(
            new RenameThingCommand(Guid.NewGuid(), "n"), () => Task.FromResult(Envelope<ThingDto>.Ok(new ThingDto(Guid.NewGuid(), 2))), default);

        var metadata = Assert.Single(_forwarder.Requests).Metadata!;
        Assert.Equal(AuditDetails.Redacted, metadata["token"]);
        Assert.DoesNotContain(metadata.Values, value => Equals(value, "must-never-leave"));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("API_KEY")]
    [InlineData("apiKey")]
    [InlineData("refresh-token")]
    [InlineData("qrPayload")]
    [InlineData("clientSecret")]
    public void Every_secret_shaped_key_is_redacted_in_metadata_and_in_before_after(string key)
    {
        var details = new AuditDetails().Set(key, "v").Change(key, "old", "new");

        Assert.Equal(AuditDetails.Redacted, details.Metadata[key]);
        Assert.Equal(AuditDetails.Redacted, details.BeforeState![key]);
        Assert.Equal(AuditDetails.Redacted, details.AfterState![key]);
    }

    [Fact]
    public async Task A_forwarder_that_throws_never_changes_the_result_but_is_warned_and_counted()
    {
        var logger = new ListLogger<AuditForwardingBehavior<RenameThingCommand, Envelope<ThingDto>>>();
        var behavior = new AuditForwardingBehavior<RenameThingCommand, Envelope<ThingDto>>(
            new RecordingForwarder { Throw = new HttpRequestException("boom") }, _counters, logger);
        var expected = Envelope<ThingDto>.Ok(new ThingDto(Guid.NewGuid(), 1));

        var response = await behavior.Handle(new RenameThingCommand(Guid.NewGuid(), "n"), () => Task.FromResult(expected), default);

        Assert.Same(expected, response);
        Assert.Equal(1, _counters.Failed);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("NOT written", StringComparison.Ordinal));
    }
}

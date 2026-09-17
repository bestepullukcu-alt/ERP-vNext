using System.Net;
using System.Text.Json;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/*
 * BL-421 — THE ACTOR OF A TENANT AUDIT APPEND IS THE AUTHENTICATED CALLER.
 *
 * WHAT WAS WRONG. POST /api/v1/platform/audit/events forced the tenant to the caller's, but recorded ActorType and
 * ActorId from the request body. Anyone holding platform.audit.events.append could write an event into their tenant's
 * trail naming a platform administrator, the system, a service or a colleague — and the trail would say they did it.
 *
 * THE RULE. The record names the token's user id with the actor type from AuditActorTypeResolver.ForCommand (BL-409).
 * A body naming any other actor is refused 400 with a stable code and NOTHING is appended; a body naming the caller,
 * or naming no actor at all, appends one event recorded as the caller.
 *
 * ONE HTTP ROUND TRIP PER CASE through the shipped controller (SignedTenantTokenHttpHost): signed tenant token → JWT
 * bearer → TenantResolutionMiddleware → [HasPermission] → PlatformAuditAppendController → production AuditService →
 * what the audit outbox holds. The accepting cases are the control: the same host and token that are refused for a
 * foreign actor DO append, so a refusal is not the host refusing everything.
 */
public sealed class AuditAppendActorHttpTests
{
    private const string AppendPath = "/api/v1/platform/audit/events";

    private static readonly Guid Tenant = Guid.Parse("42142142-0000-4000-8000-0000000000c1");
    private static readonly Guid Caller = Guid.Parse("42142142-0000-4000-8000-0000000000c2");
    private static readonly Guid Colleague = Guid.Parse("42142142-0000-4000-8000-0000000000c3");

    [Theory]
    [InlineData("PlatformAdministrator")]
    [InlineData("PartnerAdministrator")]
    [InlineData("System")]
    [InlineData("Service")]
    public async Task A_body_naming_another_actor_type_is_refused_400_and_nothing_is_appended(string actorType)
    {
        using var host = new SignedTenantTokenHttpHost();

        var response = await host.PostJsonAsync(AppendPath, CallerToken(), Body(actorType, Caller));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(GovernedAuditAppendValidation.ActorTypeMismatch, await ErrorsAsync(response));
        Assert.Empty(host.Outbox.Writes);
    }

    [Fact]
    public async Task A_body_naming_another_user_id_is_refused_400_and_nothing_is_appended()
    {
        using var host = new SignedTenantTokenHttpHost();

        var response = await host.PostJsonAsync(AppendPath, CallerToken(), Body("TenantUser", Colleague));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(GovernedAuditAppendValidation.ActorIdMismatch, await ErrorsAsync(response));
        Assert.Empty(host.Outbox.Writes);
    }

    [Fact]
    public async Task A_body_naming_the_caller_appends_one_event_recorded_as_the_caller()
    {
        using var host = new SignedTenantTokenHttpHost();

        var response = await host.PostJsonAsync(AppendPath, CallerToken(), Body("TenantUser", Caller));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertOneEventRecordedAsTheCaller(host);
    }

    [Fact]
    public async Task A_body_that_names_no_actor_appends_one_event_recorded_as_the_caller()
    {
        using var host = new SignedTenantTokenHttpHost();

        var response = await host.PostJsonAsync(AppendPath, CallerToken(), Body(actorType: null, actorId: null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertOneEventRecordedAsTheCaller(host);
    }

    private static void AssertOneEventRecordedAsTheCaller(SignedTenantTokenHttpHost host)
    {
        var write = Assert.Single(host.Outbox.Writes);
        Assert.Equal(Tenant, write.TenantId);

        var stored = InMemoryAuditOutbox.ToAuditEvent(write);
        Assert.Equal(AuditActorType.TenantUser, stored.ActorType);
        Assert.Equal(Caller, stored.ActorId);
        Assert.Equal(Tenant, stored.TenantId);
    }

    private static string CallerToken()
        => SignedTenantTokenHttpHost.TenantUserToken(Tenant, Caller, PlatformAuditAppendController.AppendPermission);

    /// <summary>The body HCM's client sends for an employee update, with the actor fields as the case needs them.</summary>
    private static string Body(string? actorType, Guid? actorId)
    {
        var body = new Dictionary<string, object?>
        {
            ["correlationId"] = Guid.NewGuid(),
            ["requestType"] = "employee.profile.updated",
            ["targetTenantId"] = Tenant,
            ["category"] = "System",
            ["entityType"] = "Employee",
            ["entityId"] = Guid.NewGuid(),
            ["operation"] = "Update",
            ["outcome"] = "Succeeded",
            ["metadata"] = new Dictionary<string, object?> { ["changed_fields"] = "employee_status" },
            ["sourceService"] = "Diten.HcmService",
            ["sourceModule"] = "MOD-0251"
        };

        if (actorType is not null)
        {
            body["actorType"] = actorType;
        }

        if (actorId is not null)
        {
            body["actorId"] = actorId;
        }

        return JsonSerializer.Serialize(body);
    }

    private static async Task<IReadOnlyList<string>> ErrorsAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(error => error.GetString() ?? string.Empty)
            .ToList();
    }
}

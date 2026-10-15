using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitQr;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3E · W3E-FIX (G-4) — the QR card and its history name the people: the institution read carries
/// <c>IssuedByName</c> / <c>RevokedByName</c> and every history row <c>ByName</c>, resolved in ONE call through the
/// production <see cref="UserDisplayNames"/> seam (the settings DTO's <c>DisplayNames</c> pattern). The resolver is
/// tenant-scoped (as AuthService's endpoint is): another tenant's user is never named, an unknown id stays null, a failed
/// call is null — the id fields are unchanged, the screen shows "—" for a null name.
/// </summary>
public sealed partial class VisitExecutionW3Tests
{
    private static readonly Guid Ayse = Guid.Parse("0a0a0a0a-0000-0000-0000-00000000000a");
    private static readonly Guid Bora = Guid.Parse("0b0b0b0b-0000-0000-0000-00000000000b");
    private static readonly Guid Foreigner = Guid.Parse("0f0f0f0f-0000-0000-0000-00000000000f");

    private VisitQrService NamedQrService(Guid actor, IUserDisplayNameResolver? names, ITenantContext? tenant = null)
        => new(tenant ?? TenantCtx(), new FixedActor(actor), _accounts, _qrCodes, _qrEvents, _qrKeys, _clock, names);

    private TenantScopedNames Names(ITenantContext tenant) => new(tenant, new()
    {
        [(Tenant, Ayse)] = "Ayşe Yılmaz",
        [(Tenant, Bora)] = "Bora Kaya",
        [(OtherTenant, Foreigner)] = "Başka Kiracı Kişisi"
    });

    [Fact]
    public async Task G4_the_card_and_the_history_name_the_issuer_and_the_revoker()
    {
        var tenant = TenantCtx();
        var names = Names(tenant);
        var account = NewAccount();

        var issued = await new IssueVisitQrHandler(NamedQrService(Ayse, names, tenant))
            .Handle(new IssueVisitQrCommand(account.Id, new VisitQrIssueInput(null, null)), default);
        Assert.Equal(201, issued.StatusCode);
        Assert.Equal(("Ayşe Yılmaz", Ayse.ToString()), (issued.Data!.IssuedByName, issued.Data.IssuedBy)); // the id field is as it was

        _clock.Value = Now.AddMinutes(5);
        var revoked = await new RevokeVisitQrHandler(NamedQrService(Bora, names, tenant))
            .Handle(new RevokeVisitQrCommand(account.Id, new VisitQrRevokeInput(1, "Etiket kayboldu")), default);
        Assert.Equal(("Ayşe Yılmaz", "Bora Kaya"), (revoked.Data!.IssuedByName, revoked.Data.RevokedByName));

        var read = await new GetVisitQrAccountHandler(NamedQrService(Bora, names, tenant)).Handle(new GetVisitQrAccountQuery(account.Id), default);
        Assert.Equal(("Ayşe Yılmaz", "Bora Kaya"), (read.Data!.IssuedByName, read.Data.RevokedByName));

        var history = (await new GetVisitQrHistoryHandler(NamedQrService(Bora, names, tenant)).Handle(new GetVisitQrHistoryQuery(account.Id), default)).Data!;
        Assert.Equal(new[] { ("revoked", "Bora Kaya"), ("issued", "Ayşe Yılmaz") }, history.Select(e => (e.Kind, e.ByName!)).ToArray());
        Assert.Equal(4, names.Calls.Count);                                                      // ONE call per answer (issue, revoke, read, history)
        Assert.Equal(new[] { Bora, Ayse }, names.Calls[^1].ToArray());                           // the history: its two people at once
    }

    [Fact]
    public async Task G4_another_tenants_user_is_never_named()
    {
        var tenant = TenantCtx();
        var names = Names(tenant);
        var account = NewAccount();

        // a code issued by someone the CURRENT tenant's directory does not know (the other tenant's user id)
        await new IssueVisitQrHandler(NamedQrService(Foreigner, names, tenant)).Handle(new IssueVisitQrCommand(account.Id, new VisitQrIssueInput(null, null)), default);

        var read = await new GetVisitQrAccountHandler(NamedQrService(Ayse, names, tenant)).Handle(new GetVisitQrAccountQuery(account.Id), default);
        var history = await new GetVisitQrHistoryHandler(NamedQrService(Ayse, names, tenant)).Handle(new GetVisitQrHistoryQuery(account.Id), default);
        Assert.Null(read.Data!.IssuedByName);
        Assert.Null(history.Data!.Single().ByName);
        var wire = JsonSerializer.Serialize(read.Data) + JsonSerializer.Serialize(history.Data);
        Assert.DoesNotContain("Başka Kiracı", wire);

        // the other tenant reading THIS institution: 404, so no name of this tenant leaves either
        var other = new TenantContext();
        other.SetTenant(OtherTenant);
        Assert.Equal(404, (await new GetVisitQrAccountHandler(NamedQrService(Foreigner, Names(other), other)).Handle(new GetVisitQrAccountQuery(account.Id), default)).StatusCode);
        Assert.Equal(404, (await new GetVisitQrHistoryHandler(NamedQrService(Foreigner, Names(other), other)).Handle(new GetVisitQrHistoryQuery(account.Id), default)).StatusCode);
    }

    [Fact]
    public async Task G4_without_a_resolver_or_when_it_fails_the_name_is_null_and_the_read_still_answers()
    {
        var account = NewAccount();
        await new IssueVisitQrHandler(NamedQrService(Ayse, null)).Handle(new IssueVisitQrCommand(account.Id, new VisitQrIssueInput(null, null)), default);

        var bare = await new GetVisitQrAccountHandler(NamedQrService(Ayse, null)).Handle(new GetVisitQrAccountQuery(account.Id), default);
        Assert.Equal((200, (string?)null), (bare.StatusCode, bare.Data!.IssuedByName));

        var failing = await new GetVisitQrHistoryHandler(NamedQrService(Ayse, new ThrowingNames())).Handle(new GetVisitQrHistoryQuery(account.Id), default);
        Assert.Equal((200, (string?)null), (failing.StatusCode, failing.Data!.Single().ByName));
    }

    private sealed class FixedActor(Guid id) : IActorContext
    {
        public string? ActorName => id.ToString();
    }

    /// <summary>As AuthService's endpoint: names only the users of the tenant the call is made for.</summary>
    private sealed class TenantScopedNames(ITenantContext tenant, Dictionary<(Guid Tenant, Guid User), string> directory) : IUserDisplayNameResolver
    {
        public List<IReadOnlyCollection<Guid>> Calls { get; } = new();

        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
        {
            Calls.Add(userIds);
            var map = userIds.Where(u => tenant.TenantId is { } t && directory.ContainsKey((t, u)))
                .ToDictionary(u => u, u => directory[(tenant.TenantId!.Value, u)]);
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(map);
        }
    }

    private sealed class ThrowingNames : IUserDisplayNameResolver
    {
        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("auth down");
    }
}

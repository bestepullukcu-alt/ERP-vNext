using System.Net;
using System.Text.Json;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S5 (CT decision 4) — the item's actions are the server's: what its status allows AND the reader holds
/// the key for, on the detail and on every list row. WITHDRAW_APPROVAL is never offered before S2.
/// </summary>
public sealed partial class ItemS1HttpTests
{
    [Theory]
    [InlineData(ItemLifecycleStatus.Draft, new[] { "DETAILS", "EDIT", "SUBMIT" })]
    [InlineData(ItemLifecycleStatus.PendingActivation, new[] { "DETAILS" })]
    [InlineData(ItemLifecycleStatus.Active, new[] { "DETAILS", "EDIT", "DEACTIVATE" })]
    [InlineData(ItemLifecycleStatus.Deactivated, new[] { "DETAILS", "REACTIVATE", "RETIRE" })]
    [InlineData(ItemLifecycleStatus.Retired, new[] { "DETAILS" })]
    public async Task S5_An_items_actions_follow_its_status(ItemLifecycleStatus status, string[] expected)
    {
        var id = await CreatedAsync("Actions " + status);
        await host.Database.GetCollection<Item>("mdm_items").UpdateOneAsync(x => x.Id == id,
            Builders<Item>.Update.Set(x => x.LifecycleStatus, status));

        Assert.Equal(expected, await DetailActionsAsync(Client(), id));
        Assert.Equal(expected, await ListActionsAsync(Client(), id));
    }

    [Fact]
    public async Task S5_An_items_actions_follow_the_readers_keys()
    {
        var id = await CreatedAsync("Actions by key");
        var reader = host.Client(host.Token(_tenant, _user, ["mdm.items.read"]));
        var editor = host.Client(host.Token(_tenant, _user, ["mdm.items.read", "mdm.items.update"]));

        Assert.Equal(["DETAILS"], await DetailActionsAsync(reader, id));
        Assert.Equal(["DETAILS"], await ListActionsAsync(reader, id));
        Assert.Equal(["DETAILS", "EDIT"], await DetailActionsAsync(editor, id));
        Assert.DoesNotContain("WITHDRAW_APPROVAL", await DetailActionsAsync(Client(), id));
    }

    private async Task<string[]> DetailActionsAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"{Items}/{id:D}");
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        using var document = JsonDocument.Parse(text);
        return Strings(document.RootElement.GetProperty("data").GetProperty("availableActions"));
    }

    private async Task<string[]> ListActionsAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"{Items}?pageSize=100");
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        using var document = JsonDocument.Parse(text);
        var row = document.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == id);
        return Strings(row.GetProperty("availableActions"));
    }

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();
}

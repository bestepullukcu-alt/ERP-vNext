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

    // S5-FIX1 (CT K1): an action is offered only to a holder of ITS key, in every status — the reader holds every item key
    // but that one, and the action is in no list (detail and list rows). The sabotage target "RETIRE held by the read key".
    public static TheoryData<ItemLifecycleStatus, string, string> EveryStatusAndActionKey()
    {
        var data = new TheoryData<ItemLifecycleStatus, string, string>();
        foreach (var status in Enum.GetValues<ItemLifecycleStatus>())
        foreach (var (action, key) in new[] { ("EDIT", "mdm.items.update"), ("SUBMIT", "mdm.items.submit"),
                     ("DEACTIVATE", "mdm.items.deactivate"), ("REACTIVATE", "mdm.items.submit"), ("RETIRE", "mdm.items.retire") })
            data.Add(status, action, key);
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryStatusAndActionKey))]
    public async Task S5_FIX1_An_action_is_never_offered_without_its_own_key(ItemLifecycleStatus status, string action, string key)
    {
        var id = await CreatedAsync($"Key {status} {action}");
        await host.Database.GetCollection<Item>("mdm_items").UpdateOneAsync(x => x.Id == id,
            Builders<Item>.Update.Set(x => x.LifecycleStatus, status));
        var withoutKey = host.Client(host.Token(_tenant, _user, All.Where(x => x != key).ToArray()));

        Assert.DoesNotContain(action, await DetailActionsAsync(withoutKey, id));
        Assert.DoesNotContain(action, await ListActionsAsync(withoutKey, id));
    }

    // S5b (BL-600): the forms' choices — the three lists in their order with the attributes the forms read (a unit's
    // dimension, a storage condition's kind).
    [Fact]
    public async Task S5b_The_options_list_the_three_item_lists_in_order()
    {
        var response = await Client().GetAsync($"{Items}/options");
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        using var document = JsonDocument.Parse(text);
        var data = document.RootElement.GetProperty("data");

        Assert.Contains("XBX", Codes(data.GetProperty("uoms")));
        Assert.Contains("INTERMEDIATE", Codes(data.GetProperty("materialTypes")));
        var cool = data.GetProperty("storageConditions").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "COOL_2_8");
        Assert.Equal("TEMPERATURE", cool.GetProperty("attributes").GetProperty("ConditionKind").GetString());
        var orders = data.GetProperty("uoms").EnumerateArray().Select(x => x.GetProperty("sortOrder").GetInt32()).ToList();
        Assert.Equal(orders.Order(), orders);
    }

    // The narrowest door: whoever may open the item form or a stock form; a read-only key does not.
    [Theory]
    [InlineData("mdm.items.read", HttpStatusCode.Forbidden)]
    [InlineData("mdm.items.create", HttpStatusCode.OK)]
    [InlineData("mdm.items.update", HttpStatusCode.OK)]
    [InlineData("mdm.gskus.update", HttpStatusCode.OK)]
    [InlineData("mdm.lskus.create", HttpStatusCode.OK)]
    [InlineData("mdm.finished-goods.create", HttpStatusCode.OK)]
    [InlineData("mdm.gskus.read", HttpStatusCode.Forbidden)]
    public async Task S5b_The_options_open_to_whoever_may_open_an_item_or_stock_form(string key, HttpStatusCode expected)
    {
        var response = await host.Client(host.Token(_tenant, _user, [key])).GetAsync($"{Items}/options");
        Assert.Equal(expected, response.StatusCode);
    }

    // The sabotage target "an outage answered as an empty list": a list that cannot be read is refused by name.
    [Fact]
    public async Task S5b_A_list_outage_is_refused_by_name_never_an_empty_list()
    {
        host.ItemReferences.Outage = true;

        var response = await Client().GetAsync($"{Items}/options");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("REFERENCE_PROVIDER_UNAVAILABLE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"uoms\"", text, StringComparison.Ordinal);
    }

    private static string[] Codes(JsonElement array) => array.EnumerateArray().Select(x => x.GetProperty("code").GetString()!).ToArray();

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

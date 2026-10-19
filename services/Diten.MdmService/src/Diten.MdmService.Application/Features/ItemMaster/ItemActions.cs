using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ItemMaster;

/// <summary>
/// MOD-0290-FU04 S5 (CT decision 4) — the item's actions are the server's (the GSKU pattern): what its status allows
/// (<see cref="ItemLifecycle"/>) AND the reader holds the key for. The screen draws only these. S2 lane merge FIX1:
/// WITHDRAW_APPROVAL is offered on a pending approval to the person who requested it AND holds mdm.items.withdraw (the
/// GSKU pattern; the endpoint refuses anyone else with ITEM_ACTIVATION_WITHDRAWAL_NOT_REQUESTER).
/// </summary>
public static class ItemActions
{
    public const string Read = "mdm.items.read";
    public const string Update = "mdm.items.update";
    public const string Submit = "mdm.items.submit";
    public const string Deactivate = "mdm.items.deactivate";
    public const string Retire = "mdm.items.retire";
    public const string Withdraw = "mdm.items.withdraw";

    public static IReadOnlyList<string> For(ItemLifecycleStatus status, IProductIdentityLifecycleActorContext actor,
        Guid? requesterSubjectId = null)
    {
        var actions = new List<string> { "DETAILS" };
        if (ItemLifecycle.Allows(status, ItemTransition.Update) && actor.HasPermission(Update)) actions.Add("EDIT");
        if (ItemLifecycle.Allows(status, ItemTransition.Submit) && actor.HasPermission(Submit)) actions.Add("SUBMIT");
        if (ItemLifecycle.Allows(status, ItemTransition.Withdraw) && requesterSubjectId is { } requester
            && actor.TryResolveCanonicalHumanSubject(out var subject) && subject == requester && actor.HasPermission(Withdraw))
            actions.Add("WITHDRAW_APPROVAL");
        if (ItemLifecycle.Allows(status, ItemTransition.Deactivate) && actor.HasPermission(Deactivate)) actions.Add("DEACTIVATE");
        if (ItemLifecycle.Allows(status, ItemTransition.RequestReactivation) && actor.HasPermission(Submit)) actions.Add("REACTIVATE");
        if (ItemLifecycle.Allows(status, ItemTransition.Retire) && actor.HasPermission(Retire)) actions.Add("RETIRE");
        return actions;
    }
}

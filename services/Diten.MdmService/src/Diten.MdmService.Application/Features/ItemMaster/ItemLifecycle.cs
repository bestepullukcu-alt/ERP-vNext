using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ItemMaster;

// MOD-0290-FU04 §8.1 (C2), the whole table: S1 applies Update / Deactivate / Retire; Submit, RequestReactivation,
// Withdraw and the decision moves are S2's (the approval), declared here so the table is one place.
public enum ItemTransition
{
    Update = 1,
    Submit = 2,
    Withdraw = 3,
    ApproveActivation = 4,
    RejectActivation = 5,
    Deactivate = 6,
    RequestReactivation = 7,
    Retire = 8
}

public static class ItemLifecycle
{
    public static bool Allows(ItemLifecycleStatus from, ItemTransition transition) => (from, transition) switch
    {
        (ItemLifecycleStatus.Draft, ItemTransition.Update) => true,
        (ItemLifecycleStatus.Active, ItemTransition.Update) => true,
        (ItemLifecycleStatus.Draft, ItemTransition.Submit) => true,
        (ItemLifecycleStatus.PendingActivation, ItemTransition.Withdraw) => true,
        (ItemLifecycleStatus.PendingActivation, ItemTransition.ApproveActivation) => true,
        (ItemLifecycleStatus.PendingActivation, ItemTransition.RejectActivation) => true,
        (ItemLifecycleStatus.Active, ItemTransition.Deactivate) => true,
        (ItemLifecycleStatus.Deactivated, ItemTransition.RequestReactivation) => true,
        (ItemLifecycleStatus.Deactivated, ItemTransition.Retire) => true,
        _ => false
    };

    // Where a move lands. A refused / withdrawn reactivation goes back to Deactivated, a first activation to Draft.
    public static ItemLifecycleStatus Target(ItemLifecycleStatus from, ItemTransition transition, ItemActivationKind? kind)
    {
        if (!Allows(from, transition)) throw new InvalidOperationException(ItemErrors.TransitionInvalid);
        return transition switch
        {
            ItemTransition.Update => from,
            ItemTransition.Submit or ItemTransition.RequestReactivation => ItemLifecycleStatus.PendingActivation,
            ItemTransition.ApproveActivation => ItemLifecycleStatus.Active,
            ItemTransition.Withdraw or ItemTransition.RejectActivation => kind == ItemActivationKind.Reactivation
                ? ItemLifecycleStatus.Deactivated
                : ItemLifecycleStatus.Draft,
            ItemTransition.Deactivate => ItemLifecycleStatus.Deactivated,
            ItemTransition.Retire => ItemLifecycleStatus.Retired,
            _ => throw new InvalidOperationException(ItemErrors.TransitionInvalid)
        };
    }
}

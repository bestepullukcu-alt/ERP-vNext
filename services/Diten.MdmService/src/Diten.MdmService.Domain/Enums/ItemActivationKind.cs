namespace Diten.MdmService.Domain.Enums;

// MOD-0290-FU04 §8.1: a reactivation is an activation; a refused / withdrawn one returns to Deactivated, not Draft.
public enum ItemActivationKind
{
    Activation = 1,
    Reactivation = 2
}

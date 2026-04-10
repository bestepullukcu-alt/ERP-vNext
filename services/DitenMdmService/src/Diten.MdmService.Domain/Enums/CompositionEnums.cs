namespace Diten.MdmService.Domain.Enums;

public enum CompositionLifecycleState
{
    Draft = 1,
    Active = 2,
    Obsolete = 3
}

public enum CompositionVersionStatus
{
    Draft = 1,
    Active = 2,
    Superseded = 3,
    Obsolete = 4
}

public enum CompositionComponentType
{
    Api = 1,
    Excipient = 2,
    Coating = 3,
    Solvent = 4,
    Preservative = 5,
    Flavor = 6
}

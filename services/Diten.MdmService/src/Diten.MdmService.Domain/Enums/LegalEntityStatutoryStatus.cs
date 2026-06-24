namespace Diten.MdmService.Domain.Enums;

// MOD-0220 FULL — statutory registration standing (distinct from operational lifecycle). Int values PERSISTED.
public enum LegalEntityStatutoryStatus
{
    Registered = 1,
    Pending = 2,
    Suspended = 3,
    Dissolved = 4
}

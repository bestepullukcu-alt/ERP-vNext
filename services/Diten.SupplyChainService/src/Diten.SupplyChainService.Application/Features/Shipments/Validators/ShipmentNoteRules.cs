using System.Text;

namespace Diten.SupplyChainService.Application.Features.Shipments.Validators;

internal static class ShipmentNoteRules
{
    public static bool IsWithinLength(string? note)
    {
        if (note is null) return true;

        // JSON Schema maxLength counts Unicode scalars, not UTF-16 units or graphemes.
        var count = 0;
        foreach (var rune in note.EnumerateRunes())
        {
            if (++count > 1000) return false;
        }
        return true;
    }
}

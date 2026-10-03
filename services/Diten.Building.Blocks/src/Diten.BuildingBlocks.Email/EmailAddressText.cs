namespace Diten.BuildingBlocks.Email;

/// <summary>
/// BL-454 — is this ONE address, fit to stand alone in a header (To, Cc, Reply-To)?
///
/// <para>A reply address comes from a tenant's settings and a recipient from a user record: neither is trusted to be
/// a single, plain address. A value with a line break would start a header of its own; a value with a comma, a
/// semicolon or angle brackets would add a second mailbox. Such a value is refused here, before any mail library
/// parses it — a library's own parser is lenient by design and decides differently from the next one.</para>
/// </summary>
public static class EmailAddressText
{
    public const int MaxLength = 254;

    public static bool IsSingleAddress(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (char.IsControl(ch) || char.IsWhiteSpace(ch) || EmailHeaderText.IsHidden(ch)
                || ch is ',' or ';' or '<' or '>' or '"' or '\\' or '(' or ')' or '[' or ']' or ':')
            {
                return false;
            }
        }

        var at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return false;
        }

        var domain = value[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.') && !domain.Contains("..");
    }
}

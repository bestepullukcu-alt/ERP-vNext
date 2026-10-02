namespace TenantArchitecture.ArchitectureTests.AuditTrailStandard;

/*
 * THE LEDGER — one human-readable file per service under `tests/architecture/audit-ledger/`.
 *
 * It holds ONLY declarations a person has to make; nothing in it is counted as evidence by itself:
 *   ## İzler         which audit mechanisms the service has (name → path a/b/c/aday → the production identifier)
 *   ## İstisnalar    commands that are deliberately not audited (class from the rule file + a written reason)
 *   ## Dolaylı       "this handler reaches the trail through that type" — PROVEN against production code
 *   ## Bilinen borç  commands that are not audited today. This list may only SHRINK.
 *
 * Whether a command IS audited is never read from here. It is read from `services/<svc>/src`.
 */
internal enum TrailKind
{
    /// <summary>The identifier is an interface on the COMMAND type; a pipeline behavior does the writing.</summary>
    Marker,

    /// <summary>The identifier is named in the command's HANDLER; the handler does the writing.</summary>
    Writer
}

internal sealed record TrailDeclaration(string Name, string Path, TrailKind Kind, IReadOnlyList<string> Required, IReadOnlyList<string> Forbidden)
{
    /// <summary>a = Platform in-process, b = forwarded to the central log, c = equivalent trail. `aday` is NOT accepted.</summary>
    public bool IsAccepted => Path is "a" or "b" or "c";
}

internal sealed record ExceptionDeclaration(string Command, string Class, string Reason);

internal sealed record IndirectDeclaration(string Command, string Trail, string Via);

internal sealed class AuditLedger
{
    public const string TrailsHeading = "## İzler";
    public const string ExceptionsHeading = "## İstisnalar";
    public const string IndirectHeading = "## Dolaylı";
    public const string DebtHeading = "## Bilinen borç";

    public static readonly string[] ValidPaths = ["a", "b", "c", "aday"];

    public required string Service { get; init; }
    public required string RelativePath { get; init; }
    public List<TrailDeclaration> Trails { get; } = [];
    public List<ExceptionDeclaration> Exceptions { get; } = [];
    public List<IndirectDeclaration> Indirect { get; } = [];
    public List<string> Debt { get; } = [];

    /// <summary>Lines the parser could not place. A typo in a heading must not silently empty a section.</summary>
    public List<string> Malformed { get; } = [];

    public static AuditLedger Parse(string service, string relativePath, string content)
    {
        var ledger = new AuditLedger { Service = service, RelativePath = relativePath };
        string? section = null;
        var rowsSeenInSection = 0;

        foreach (var rawLine in content.Replace("\r", string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                section = line;
                rowsSeenInSection = 0;
                if (section is not (TrailsHeading or ExceptionsHeading or IndirectHeading or DebtHeading))
                {
                    ledger.Malformed.Add($"bilinmeyen bölüm başlığı: '{line}'");
                }

                continue;
            }

            if (section is null || line.Length == 0)
            {
                continue;
            }

            if (section == DebtHeading)
            {
                if (line.StartsWith("- ", StringComparison.Ordinal))
                {
                    var name = line[2..].Trim();
                    if (name.Length == 0 || name.Any(char.IsWhiteSpace))
                    {
                        ledger.Malformed.Add($"borç satırı tek bir komut adı olmalı: '{line}'");
                    }
                    else
                    {
                        ledger.Debt.Add(name);
                    }
                }

                continue;
            }

            if (!line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
            if (cells.All(cell => cell.Length > 0 && cell.All(ch => ch is '-' or ':')))
            {
                continue; // the |---|---| separator
            }

            rowsSeenInSection++;
            if (rowsSeenInSection == 1)
            {
                continue; // the header row
            }

            switch (section)
            {
                case TrailsHeading when cells.Length == 4:
                    var kind = cells[2] switch
                    {
                        "işaret" => TrailKind.Marker,
                        "yazıcı" => TrailKind.Writer,
                        _ => (TrailKind?)null
                    };
                    var tokens = cells[3].Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (kind is null || !ValidPaths.Contains(cells[1]) || cells[0].Length == 0 || tokens.All(t => t.StartsWith('!')))
                    {
                        ledger.Malformed.Add($"iz satırı okunamadı (yol a|b|c|aday, tür işaret|yazıcı, en az bir belirteç): '{line}'");
                        break;
                    }

                    ledger.Trails.Add(new TrailDeclaration(
                        cells[0],
                        cells[1],
                        kind.Value,
                        tokens.Where(t => !t.StartsWith('!')).ToArray(),
                        tokens.Where(t => t.StartsWith('!')).Select(t => t[1..]).ToArray()));
                    break;

                case ExceptionsHeading when cells.Length == 3:
                    ledger.Exceptions.Add(new ExceptionDeclaration(cells[0], cells[1], cells[2]));
                    break;

                case IndirectHeading when cells.Length == 3:
                    ledger.Indirect.Add(new IndirectDeclaration(cells[0], cells[1], cells[2]));
                    break;

                default:
                    ledger.Malformed.Add($"satırın sütun sayısı bölümüne uymuyor: '{line}'");
                    break;
            }
        }

        return ledger;
    }
}

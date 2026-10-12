namespace TenantArchitecture.ArchitectureTests.AuditTrailStandard;

/*
 * THE LEDGER — one human-readable file per service under `tests/architecture/audit-ledger/`.
 *
 * It holds ONLY declarations a person has to make; nothing in it is counted as evidence by itself:
 *   ## İzler           which audit mechanisms the service has (name → path a/b/c/aday → the production identifier)
 *   ## İstisnalar      commands that are deliberately not audited (class from the rule file + a written reason)
 *   ## Dolaylı         "this handler reaches the trail through that type" — PROVEN against production code
 *   ## Bilinen borç    commands that are not audited today. This list may only SHRINK.
 *   ## K2 borcu        commands that ARE audited but whose audit is best-effort although the owner decided their
 *                      class fails closed (rule §4.3). May only shrink.
 *   ## Yazan sorgular  queries whose handler writes (the query rule hides them from every other list).
 *
 * Whether a command IS audited is never read from here. It is read from `services/<svc>/src`.
 *
 * ⚠ THE PARSER IS STRICT ON PURPOSE. A line it cannot place is reported with file:line instead of being skipped:
 * a debt entry typed as `* X` or `-X` used to vanish silently, which made the command "new and unaudited" in one
 * run and, worse, let a typo'd heading empty a whole section.
 */
internal enum TrailKind
{
    /// <summary>The identifier is an interface on the COMMAND type; a pipeline behavior does the writing.</summary>
    Marker,

    /// <summary>The identifier is a WRITE MEMBER (<c>Type.Method</c>) the command's HANDLER calls.</summary>
    Writer
}

/// <summary>
/// <c>A+B</c> = both required · <c>A/B</c> = either (a `|` would end the markdown cell) · <c>!C</c> = must be absent. For a writer every positive token is
/// <c>Type.Method</c>: naming the type is not evidence — a handler that only READS through it would be credited.
/// </summary>
internal sealed record TrailDeclaration(
    string Name,
    string Path,
    TrailKind Kind,
    IReadOnlyList<IReadOnlyList<string>> RequiredGroups,
    IReadOnlyList<string> Forbidden,
    string RawToken)
{
    /// <summary>a = Platform in-process, b = forwarded to the central log, c = equivalent trail. `aday` is NOT accepted.</summary>
    public bool IsAccepted => Path is "a" or "b" or "c";

    public IEnumerable<string> PositiveTokens => RequiredGroups.SelectMany(group => group);
}

internal sealed record ExceptionDeclaration(string Command, string Class, string Reason);

internal sealed record IndirectDeclaration(string Command, string Trail, string Via);

internal sealed class AuditLedger
{
    public const string TrailsHeading = "## İzler";
    public const string ExceptionsHeading = "## İstisnalar";
    public const string IndirectHeading = "## Dolaylı";
    public const string DebtHeading = "## Bilinen borç";
    public const string K2DebtHeading = "## K2 borcu";
    public const string WritingQueriesHeading = "## Yazan sorgular";

    private static readonly string[] ListHeadings = [DebtHeading, K2DebtHeading, WritingQueriesHeading];
    private static readonly string[] TableHeadings = [TrailsHeading, ExceptionsHeading, IndirectHeading];

    public static readonly string[] ValidPaths = ["a", "b", "c", "aday"];

    public required string Service { get; init; }
    public required string RelativePath { get; init; }
    public List<TrailDeclaration> Trails { get; } = [];
    public List<ExceptionDeclaration> Exceptions { get; } = [];
    public List<IndirectDeclaration> Indirect { get; } = [];
    public List<string> Debt { get; } = [];
    public List<string> K2Debt { get; } = [];
    public List<string> WritingQueries { get; } = [];

    /// <summary>Lines the parser could not place, each with its file:line.</summary>
    public List<string> Malformed { get; } = [];

    public static AuditLedger Parse(string service, string relativePath, string content)
    {
        var ledger = new AuditLedger { Service = service, RelativePath = relativePath };
        string? section = null;
        var rowsSeenInSection = 0;
        var lineNumber = 0;

        foreach (var rawLine in content.Replace("\r", string.Empty).Split('\n'))
        {
            lineNumber++;
            var line = rawLine.Trim();
            void Unreadable(string why) => ledger.Malformed.Add($"okunamayan satır: {relativePath}:{lineNumber} — {why}: '{line}'");

            if (line.StartsWith('#'))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal) && (ListHeadings.Contains(line) || TableHeadings.Contains(line)))
                {
                    section = line;
                    rowsSeenInSection = 0;
                }
                else if (lineNumber > 1 || !line.StartsWith("# ", StringComparison.Ordinal))
                {
                    // `###`, a second `#` title, or a `##` this parser does not know: any of them would otherwise
                    // swallow the lines below it into the wrong section — or into none.
                    Unreadable("bilinmeyen başlık (yalnız altı bölüm başlığı tanınır)");
                }

                continue;
            }

            if (section is null || line.Length == 0 || line.StartsWith('>'))
            {
                continue; // prose above the first section, blank lines, and `> ` notes
            }

            if (ListHeadings.Contains(section))
            {
                var looksLikeAnEntry = line[0] is '-' or '*' or '+' || (line[0] == '|');
                if (!looksLikeAnEntry)
                {
                    continue; // a sentence
                }

                var name = line.StartsWith("- ", StringComparison.Ordinal) ? line[2..].Trim() : null;
                if (name is null || name.Length == 0 || name.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_'))
                    || char.IsWhiteSpace(rawLine[0]))
                {
                    Unreadable("liste satırı tam olarak '- TürAdı' biçiminde olmalı (girintisiz, tek ad)");
                    continue;
                }

                (section switch
                {
                    DebtHeading => ledger.Debt,
                    K2DebtHeading => ledger.K2Debt,
                    _ => ledger.WritingQueries
                }).Add(name);
                continue;
            }

            if (!line.StartsWith('|'))
            {
                if (line[0] is '-' or '*' or '+')
                {
                    Unreadable("tablo bölümünde madde işareti — not yazacaksan satırı '> ' ile başlat");
                }

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
                    var parts = cells[3].Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var groups = parts.Where(p => !p.StartsWith('!'))
                        .Select(p => (IReadOnlyList<string>)p.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .ToList();
                    if (kind is null || !ValidPaths.Contains(cells[1]) || cells[0].Length == 0 || groups.Count == 0)
                    {
                        Unreadable("iz satırı: yol a|b|c|aday, tür işaret|yazıcı, en az bir belirteç");
                        break;
                    }

                    ledger.Trails.Add(new TrailDeclaration(
                        cells[0],
                        cells[1],
                        kind.Value,
                        groups,
                        parts.Where(p => p.StartsWith('!')).Select(p => p[1..]).ToArray(),
                        cells[3]));
                    break;

                case ExceptionsHeading when cells.Length == 3:
                    ledger.Exceptions.Add(new ExceptionDeclaration(cells[0], cells[1], cells[2]));
                    break;

                case IndirectHeading when cells.Length == 3:
                    ledger.Indirect.Add(new IndirectDeclaration(cells[0], cells[1], cells[2]));
                    break;

                default:
                    Unreadable("satırın sütun sayısı bölümüne uymuyor");
                    break;
            }
        }

        return ledger;
    }
}

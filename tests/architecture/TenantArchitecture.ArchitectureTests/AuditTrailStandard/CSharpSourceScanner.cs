using System.Text;
using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests.AuditTrailStandard;

/*
 * A TEXT scanner for C# type declarations — the same method every guard in this project uses (no project
 * reference, no reflection: the architecture test project must stay buildable while a service is broken).
 *
 * WHAT IT READS: every `class` / `record` / `struct` / `interface` declaration under `services/<svc>/src`, with
 * its base list and its body, AFTER comments and string literals are removed — so a command cannot be made
 * "audited" by mentioning a marker in a comment or a string.
 *
 * WHAT IT CANNOT SEE (stated here so nobody has to discover it):
 *   - it does not resolve symbols: `IRequest` is matched by NAME, the last segment of the base type;
 *   - one namespace per file (the first one) — a file with two namespace blocks is attributed to the first;
 *   - a base list inherited from a base CLASS is not followed (only interfaces declared in the same service are
 *     followed transitively); a command that gets `IRequest` from an abstract base record is invisible.
 */
internal sealed record SourceType(
    string RelativePath,
    string Kind,
    string Name,
    string Namespace,
    bool IsAbstract,
    IReadOnlyList<string> Bases,
    string Body)
{
    /// <summary>Base type names without generic arguments and without namespace qualification.</summary>
    public IEnumerable<string> BaseNames => Bases.Select(CSharpSourceScanner.SimpleName);
}

internal static class CSharpSourceScanner
{
    private static readonly Regex Declaration = new(
        @"\b(class|record|struct|interface|enum)\s+(?:(?:class|struct)\s+)?([A-Z_]\w*)",
        RegexOptions.Compiled);

    private static readonly Regex NamespaceDeclaration = new(@"\bnamespace\s+([\w.]+)", RegexOptions.Compiled);

    public static List<SourceType> ScanDirectory(string repoRoot, string directory)
    {
        var result = new List<SourceType>();
        if (!Directory.Exists(directory))
        {
            return result;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.Contains("/obj/", StringComparison.Ordinal) || normalized.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
            result.AddRange(ScanSource(relative, File.ReadAllText(path)));
        }

        return result;
    }

    public static IEnumerable<SourceType> ScanSource(string relativePath, string rawSource)
    {
        var s = StripCommentsAndStrings(rawSource);
        var namespaceMatch = NamespaceDeclaration.Match(s);
        var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : string.Empty;

        foreach (Match match in Declaration.Matches(s))
        {
            var kind = match.Groups[1].Value;
            var name = match.Groups[2].Value;
            var i = SkipWhitespace(s, match.Index + match.Length);

            if (i < s.Length && s[i] == '<')
            {
                i = SkipWhitespace(s, SkipBalanced(s, i, '<', '>'));
            }

            // Primary constructor parameters are part of what the type "says": a handler written as
            // `class Handler(IAuditWriter audit) : IRequestHandler<...>` names its dependencies only there.
            var parametersStart = i;
            if (i < s.Length && s[i] == '(')
            {
                i = SkipBalanced(s, i, '(', ')');
            }

            var parameters = s[parametersStart..i];
            i = SkipWhitespace(s, i);

            var bases = new List<string>();
            var j = i;
            if (i < s.Length && s[i] == ':')
            {
                j = i + 1;
                var depth = 0;
                while (j < s.Length)
                {
                    var ch = s[j];
                    if (ch is '<' or '(')
                    {
                        depth++;
                    }
                    else if (ch is '>' or ')')
                    {
                        depth--;
                    }
                    else if (depth == 0 && (ch is '{' or ';' || IsWhereClause(s, j)))
                    {
                        break;
                    }

                    j++;
                }

                bases = SplitTopLevel(s[(i + 1)..j]);
            }

            var k = j;
            while (k < s.Length && s[k] is not ('{' or ';'))
            {
                k++;
            }

            var body = string.Empty;
            if (k < s.Length && s[k] == '{')
            {
                body = s[k..SkipBalanced(s, k, '{', '}')];
            }

            yield return new SourceType(relativePath, kind, name, ns, IsAbstract(s, match.Index), bases, parameters + body);
        }
    }

    public static string SimpleName(string typeReference)
    {
        var withoutGenerics = typeReference;
        var lt = withoutGenerics.IndexOf('<');
        if (lt >= 0)
        {
            withoutGenerics = withoutGenerics[..lt];
        }

        var paren = withoutGenerics.IndexOf('(');
        if (paren >= 0)
        {
            withoutGenerics = withoutGenerics[..paren];
        }

        var dot = withoutGenerics.LastIndexOf('.');
        return (dot >= 0 ? withoutGenerics[(dot + 1)..] : withoutGenerics).Trim();
    }

    /// <summary>First generic argument of a type reference, as a simple name: <c>IRequestHandler&lt;A.B, C&gt;</c> → <c>B</c>.</summary>
    public static string? FirstGenericArgument(string typeReference)
    {
        var lt = typeReference.IndexOf('<');
        if (lt < 0)
        {
            return null;
        }

        var inner = typeReference[(lt + 1)..];
        var arguments = SplitTopLevel(inner.EndsWith('>') ? inner[..^1] : inner);
        return arguments.Count == 0 ? null : SimpleName(arguments[0]);
    }

    public static bool ContainsIdentifier(string text, string identifier)
    {
        var index = 0;
        while ((index = text.IndexOf(identifier, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 || !IsIdentifierChar(text[index - 1]);
            var afterIndex = index + identifier.Length;
            var after = afterIndex >= text.Length || !IsIdentifierChar(text[afterIndex]);
            if (before && after)
            {
                return true;
            }

            index = afterIndex;
        }

        return false;
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool IsWhereClause(string s, int index) =>
        string.CompareOrdinal(s, index, "where ", 0, 6) == 0 && (index == 0 || !IsIdentifierChar(s[index - 1]));

    private static bool IsAbstract(string s, int declarationIndex)
    {
        var start = declarationIndex;
        while (start > 0 && s[start - 1] is not (';' or '{' or '}' or ']'))
        {
            start--;
        }

        return ContainsIdentifier(s[start..declarationIndex], "abstract");
    }

    private static int SkipWhitespace(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i]))
        {
            i++;
        }

        return i;
    }

    private static int SkipBalanced(string s, int i, char open, char close)
    {
        var depth = 0;
        while (i < s.Length)
        {
            if (s[i] == open)
            {
                depth++;
            }
            else if (s[i] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return i + 1;
                }
            }

            i++;
        }

        return i;
    }

    private static List<string> SplitTopLevel(string list)
    {
        var parts = new List<string>();
        var depth = 0;
        var current = new StringBuilder();
        foreach (var ch in list)
        {
            if (ch is '<' or '(')
            {
                depth++;
            }
            else if (ch is '>' or ')')
            {
                depth--;
            }

            if (ch == ',' && depth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.ToString().Trim().Length > 0)
        {
            parts.Add(current.ToString().Trim());
        }

        return parts;
    }

    /// <summary>
    /// Removes comments and replaces every string / char literal with an empty one. Braces, parentheses and
    /// angle brackets inside literals would otherwise unbalance the body extraction, and a marker named in a
    /// comment would otherwise count as evidence.
    /// </summary>
    public static string StripCommentsAndStrings(string src)
    {
        var output = new StringBuilder(src.Length);
        var i = 0;
        var n = src.Length;
        while (i < n)
        {
            var c = src[i];

            if (c == '/' && i + 1 < n && src[i + 1] == '/')
            {
                var end = src.IndexOf('\n', i);
                i = end < 0 ? n : end;
                continue;
            }

            if (c == '/' && i + 1 < n && src[i + 1] == '*')
            {
                var end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? n : end + 2;
                output.Append(' ');
                continue;
            }

            if (c == '"')
            {
                if (i + 2 < n && src[i + 1] == '"' && src[i + 2] == '"')
                {
                    var quotes = 0;
                    while (i + quotes < n && src[i + quotes] == '"')
                    {
                        quotes++;
                    }

                    var terminator = new string('"', quotes);
                    var end = src.IndexOf(terminator, i + quotes, StringComparison.Ordinal);
                    i = end < 0 ? n : end + quotes;
                    output.Append("\"\"");
                    continue;
                }

                var verbatim = i > 0 && (src[i - 1] == '@' || (src[i - 1] == '$' && i > 1 && src[i - 2] == '@'));
                var j = i + 1;
                while (j < n)
                {
                    if (verbatim)
                    {
                        if (src[j] == '"')
                        {
                            if (j + 1 < n && src[j + 1] == '"')
                            {
                                j += 2;
                                continue;
                            }

                            break;
                        }
                    }
                    else
                    {
                        if (src[j] == '\\')
                        {
                            j += 2;
                            continue;
                        }

                        if (src[j] is '"' or '\n')
                        {
                            break;
                        }
                    }

                    j++;
                }

                i = j + 1;
                output.Append("\"\"");
                continue;
            }

            if (c == '\'')
            {
                var j = i + 1;
                if (j < n && src[j] == '\\')
                {
                    j += 2;
                    while (j < n && src[j] != '\'' && j - i < 12)
                    {
                        j++;
                    }
                }
                else
                {
                    j++;
                }

                if (j < n && src[j] == '\'')
                {
                    i = j + 1;
                    output.Append("' '");
                    continue;
                }
            }

            output.Append(c);
            i++;
        }

        return output.ToString();
    }
}

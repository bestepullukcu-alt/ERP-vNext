using System.Globalization;

namespace Diten.BuildingBlocks.ListExport;

/// <summary>One exportable column: its wire key (the list DTO's field name), its header and its cell, both in a culture.</summary>
public sealed class ListExportColumn<T>
{
    public ListExportColumn(string key, Func<CultureInfo, string> header, Func<T, CultureInfo, string?> value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Header = header ?? throw new ArgumentNullException(nameof(header));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string Key { get; }
    public Func<CultureInfo, string> Header { get; }
    public Func<T, CultureInfo, string?> Value { get; }
}

/// <summary>
/// THE COLUMN WHITELIST of one list's export. The caller names the columns it shows (<c>columns=</c>); only declared keys
/// resolve, in the CALLER's order (the screen's column order), each once. An unknown key refuses the whole request —
/// a typo must not turn into a file that silently lacks a column, and a hidden field (TenantId, a password hash) can never
/// be asked into one because it is simply not declared here.
/// </summary>
public sealed class ListExportColumnSet<T>
{
    private readonly IReadOnlyList<ListExportColumn<T>> _columns;
    private readonly Dictionary<string, ListExportColumn<T>> _byKey;

    public ListExportColumnSet(IEnumerable<ListExportColumn<T>> columns)
    {
        _columns = columns.ToList();
        _byKey = new Dictionary<string, ListExportColumn<T>>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in _columns)
        {
            if (!_byKey.TryAdd(column.Key, column))
                throw new ArgumentException($"Duplicate export column key '{column.Key}'.", nameof(columns));
        }
    }

    /// <summary>The declared keys, in declaration order — what "no columns given" exports.</summary>
    public IReadOnlyList<string> Keys => _columns.Select(c => c.Key).ToList();

    /// <summary>
    /// <paramref name="requested"/> may repeat the parameter (<c>columns=a&amp;columns=b</c>) or comma-separate it
    /// (<c>columns=a,b</c>). Blank entries are ignored; nothing left = every declared column.
    /// </summary>
    public bool TryResolve(IEnumerable<string?>? requested, out IReadOnlyList<ListExportColumn<T>> columns, out string? error)
    {
        var keys = (requested ?? [])
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();

        if (keys.Count == 0)
        {
            columns = _columns;
            error = null;
            return true;
        }

        var resolved = new List<ListExportColumn<T>>();
        foreach (var key in keys)
        {
            if (!_byKey.TryGetValue(key, out var column))
            {
                columns = [];
                error = $"columns may only name: {string.Join(", ", Keys)}.";
                return false;
            }

            if (!resolved.Contains(column)) resolved.Add(column);
        }

        columns = resolved;
        error = null;
        return true;
    }
}

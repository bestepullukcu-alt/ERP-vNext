using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;

namespace Diten.CrmService.Application.Features.VisitWorkspace;

/// <summary>
/// W2-BE-d — the labels of reference values on the workspace cards (specialty, city, district), in the request's
/// language by the 4I rule: the value's <c>label_&lt;lang&gt;</c> attribute, else its display name. ONE published-values
/// read per set and request; no label is written in code. A set that is not published (or a value not in it) has no
/// label — the caller leaves the field empty.
/// </summary>
public sealed class WorkspaceReferenceLabels
{
    private readonly IReferenceDataCatalogReader? _catalog;
    private readonly Dictionary<string, IReadOnlyDictionary<string, ReferenceValueSnapshot>> _sets = new(StringComparer.Ordinal);

    public WorkspaceReferenceLabels(IReferenceDataCatalogReader? catalog) => _catalog = catalog;

    /// <summary>Reads <paramref name="setCode"/> once (only when some code needs it).</summary>
    public async Task LoadAsync(string setCode, bool needed, CancellationToken cancellationToken)
    {
        if (!needed || _catalog is null || _sets.ContainsKey(setCode))
        {
            return;
        }

        var set = await _catalog.GetPublishedValuesAsync(setCode, cancellationToken);
        _sets[setCode] = set.IsPublished
            ? set.Values.GroupBy(v => v.ValueCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ReferenceValueSnapshot>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The label of <paramref name="code"/> in <paramref name="language"/> (two letters), or null.</summary>
    public string? Label(string setCode, string? code, string language)
        => !string.IsNullOrWhiteSpace(code) && _sets.TryGetValue(setCode, out var values) && values.TryGetValue(code.Trim(), out var value)
            ? GetVisitReasonsHandler.LabelOf(value, language)
            : null;
}

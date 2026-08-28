namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// ONE record another module offers for selection. This record is the whole contract, and it was written before
/// the first provider on purpose: the expensive failure here is not a wrong query, it is two modules answering
/// in two shapes, which is how WC-1's projection layer had to be written twice.
///
/// <para>Four parts, and every consumer gets exactly these four:</para>
/// <list type="bullet">
/// <item><b>Id</b> — the identity. This is what a task STORES, so a renamed record stays the same record.</item>
/// <item><b>Code</b> — the business key. This is what the reader recognises: "QA-01", not a GUID (BL-049).</item>
/// <item><b>Name</b> — the display name.</item>
/// <item><b>Secondary</b> — an OPTIONAL second line that disambiguates. Two facilities can each own a
/// "QA Specialist"; without the unit beside it the two entries are the same word twice. Null when the name and
/// the key already say everything.</item>
/// </list>
///
/// <para>Id is a STRING rather than a Guid because the contract must survive a module whose keys are not Guids.
/// Nothing in the resolution path parses it; only the owning source interprets its own identities.</para>
/// </summary>
public sealed record TaskRecordDto(string Id, string Code, string Name, string? Secondary);

/// <summary>
/// A module that lets configurable task fields point at its records — SAP's check table, Oracle's
/// table-validated value set, ServiceNow's reference field, under this codebase's own names.
///
/// <para>Implementing this interface is the WHOLE registration. There is no switch to extend, no key to add to a
/// list, and no consumer that names a source: the registry below is built from whatever is in the container, and
/// every caller reaches a source through <see cref="ITaskRecordSourceRegistry"/>. When the Product module
/// arrives it adds a class and a DI line, and no existing line changes — which is the only test of whether this
/// is a contract or a pair of special cases.</para>
/// </summary>
public interface ITaskRecordSource
{
    /// <summary>
    /// Stable, lowercase-dashed key an administrator picks on the field-definition screen and the definition
    /// then stores in <c>OptionsSourceKey</c>. It is data, not a display string: never localised, never renamed.
    /// </summary>
    string SourceKey { get; }

    /// <summary>The module that owns these records, so the picker can say where the values come from.</summary>
    string ModuleCode { get; }

    /// <summary>
    /// Resource key naming the SOURCE itself ("Organization units"), translated in all seven languages. A source
    /// is ours, not a tenant's words, so it carries a key rather than text — the same split
    /// <c>TaskFieldDefinition</c> already makes between a system label and a tenant label.
    /// </summary>
    string LabelResourceKey { get; }

    /// <summary>
    /// Records matching <paramref name="term"/>, capped at <paramref name="take"/>.
    ///
    /// <para>The cap is the reason this is a SEARCH and not a list: a source with five thousand records cannot be
    /// poured into a dropdown, and a truncated dropdown that does not say it is truncated is worse than one that
    /// asks the user to type. An empty or null term returns the first page, so the picker opens with something in
    /// it rather than an empty box.</para>
    /// </summary>
    Task<IReadOnlyList<TaskRecordDto>> SearchAsync(string? term, int take, CancellationToken ct);

    /// <summary>
    /// Resolve identities already stored on a task back into records, for the EDIT form.
    ///
    /// <para>Without this the round trip loses data: a task saved months ago points at a record no longer on the
    /// first page, and a picker that cannot render that identity posts back a different one. Ids that no longer
    /// resolve are simply absent from the result — the caller decides what to say, and what it must never say is
    /// the raw identity.</para>
    /// </summary>
    Task<IReadOnlyList<TaskRecordDto>> ResolveAsync(IReadOnlyCollection<string> ids, CancellationToken ct);
}

/// <summary>
/// How an option source's own name is translated. Platform ships no tenant resource files, so the SOURCE carries
/// a stable key and the frontend's seven resx files carry the words — the same bridge the tenant navigation
/// already uses for module and page names.
///
/// <para>The convention is deliberate: the key is the source key with one prefix, so adding a source means adding
/// SEVEN RESX LINES and nothing else. The screen enumerates whatever carries the prefix; no list of sources
/// exists in the frontend to fall out of step.</para>
/// </summary>
public static class TaskFieldOptionSourceLabels
{
    public const string Prefix = "OptionSource.";

    public static string KeyFor(string sourceKey) => Prefix + sourceKey;
}

/// <summary>How many records one search may answer with. A cap the server owns, not the caller.</summary>
public static class TaskRecordSearchLimits
{
    public const int DefaultTake = 20;
    public const int MaxTake = 50;

    public static int Clamp(int? take) => take is null or < 1
        ? DefaultTake
        : Math.Min(take.Value, MaxTake);
}

/// <summary>
/// Every registered record source, looked up by key. A DICTIONARY built from the container — deliberately not a
/// switch, because a switch is the line that would have to be edited when the third source arrives.
/// </summary>
public interface ITaskRecordSourceRegistry
{
    IReadOnlyList<ITaskRecordSource> All { get; }

    /// <summary>The source with this key, or null. Null is an answer the callers act on, never an exception.</summary>
    ITaskRecordSource? Find(string? sourceKey);
}

public sealed class TaskRecordSourceRegistry : ITaskRecordSourceRegistry
{
    private readonly Dictionary<string, ITaskRecordSource> _byKey;

    public TaskRecordSourceRegistry(IEnumerable<ITaskRecordSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _byKey = new Dictionary<string, ITaskRecordSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            // A duplicate key means two modules claim the same name, and whichever won would be decided by
            // registration order — a bug that appears months later as "the wrong list". Refused at startup.
            if (!_byKey.TryAdd(source.SourceKey, source))
            {
                throw new InvalidOperationException(
                    $"Two task record sources claim the key '{source.SourceKey}': "
                    + $"{_byKey[source.SourceKey].GetType().Name} and {source.GetType().Name}.");
            }
        }

        All = _byKey.Values
            .OrderBy(source => source.ModuleCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.SourceKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ITaskRecordSource> All { get; }

    public ITaskRecordSource? Find(string? sourceKey) =>
        string.IsNullOrWhiteSpace(sourceKey) ? null
            : _byKey.TryGetValue(sourceKey.Trim(), out var source) ? source
            : null;
}

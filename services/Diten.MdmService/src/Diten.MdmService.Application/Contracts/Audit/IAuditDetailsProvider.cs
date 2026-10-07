namespace Diten.MdmService.Application.Contracts.Audit;

/// <summary>
/// MOD-0290-FU04 S1 FIX1 item 2 (CT decision; AUD-001 §3 #10): an <see cref="IAuditableCommand"/> that also says WHAT
/// changed — before / after values of the changed fields and metadata (reason, refusal, first fill). The handler fills
/// <see cref="AuditDetails"/> during its own execution; <c>AuditForwardingBehavior</c> reads it after the handler and
/// adds a refused command's response code as <c>refusalCode</c>. Opt-in: a command without it forwards exactly what it
/// did before (BL-574: the other path-b commands adopt it separately).
/// </summary>
public interface IAuditDetailsProvider
{
    AuditDetails AuditDetails { get; }
}

/// <summary>Per-execution audit facts. Never a password, token or personal datum (the item carries none).</summary>
public sealed class AuditDetails
{
    public const string RefusalCodeKey = "refusalCode";

    public Dictionary<string, object?>? BeforeState { get; private set; }
    public Dictionary<string, object?>? AfterState { get; private set; }
    public Dictionary<string, object?> Metadata { get; } = new(StringComparer.Ordinal);

    public void Change(string field, object? before, object? after)
    {
        (BeforeState ??= new(StringComparer.Ordinal))[field] = before;
        (AfterState ??= new(StringComparer.Ordinal))[field] = after;
    }

    // A new row: its values, no "before".
    public void Created(string field, object? value) => (AfterState ??= new(StringComparer.Ordinal))[field] = value;

    public void Set(string key, object? value) => Metadata[key] = value;
}

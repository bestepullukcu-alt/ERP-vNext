namespace Diten.Platform.Application.Features.Workflow;

/// <summary>
/// WP-0023-FX — one comparison for "is this the same person" across the workflow runtime.
///
/// <para>Principals reach the runtime in several spellings: the JWT <c>sub</c> as <c>Guid.ToString()</c> (lower-case
/// "D"), a template candidate written by hand (<c>user:5F00…</c>), a braced or upper-case id typed into a form. As
/// plain ordinal text those are different people, which is how a correct approver could be refused and — worse —
/// how the four-eyes rule could miss the starter. Two values that both parse as GUIDs are compared as GUIDs; anything
/// else (legacy free-text principals, e-mail) is compared case-insensitively. A blank value is never anybody.</para>
/// </summary>
public static class WorkflowPrincipal
{
    public static bool Same(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var a = left.Trim();
        var b = right.Trim();
        if (Guid.TryParse(a, out var leftId) && Guid.TryParse(b, out var rightId))
        {
            return leftId == rightId;
        }

        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}

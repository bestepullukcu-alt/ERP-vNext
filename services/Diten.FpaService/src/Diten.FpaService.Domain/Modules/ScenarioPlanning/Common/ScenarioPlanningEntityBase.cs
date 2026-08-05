using System.Text;

namespace Diten.FpaService.Domain.Modules.ScenarioPlanning.Common;

public abstract class ScenarioPlanningEntityBase
{
    public Guid Id { get; protected init; }
    public Guid TenantId { get; protected init; }
    public bool IsDeleted { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected init; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public int Version { get; protected set; }
}

public sealed class ScenarioPlanningDomainException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

internal static class ScenarioGuard
{
    public static Guid Required(Guid value, string code) => value == Guid.Empty ? throw new ScenarioPlanningDomainException(code) : value;
    public static string Text(string? value, int max, string code)
    {
        var normalized = value?.Trim().Normalize(NormalizationForm.FormC) ?? string.Empty;
        return normalized.Length is 0 || normalized.Length > max ? throw new ScenarioPlanningDomainException(code) : normalized;
    }
}

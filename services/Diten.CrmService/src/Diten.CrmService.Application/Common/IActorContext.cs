namespace Diten.CrmService.Application.Common;

/// <summary>
/// Who is performing the current request, for provenance fields (CreatedBy/UpdatedBy). Resolved server-side from the
/// caller principal — never from a request payload. Returns null when the principal carries no usable identity; a
/// provenance field is then left null rather than filled with a guess.
/// </summary>
public interface IActorContext
{
    string? ActorName { get; }
}

/// <summary>Default seam used by tests and non-HTTP hosts: no actor.</summary>
public sealed class NullActorContext : IActorContext
{
    public string? ActorName => null;
}

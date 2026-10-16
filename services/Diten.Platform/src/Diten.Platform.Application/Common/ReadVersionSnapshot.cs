using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Application.Common;

/// <summary>
/// BL-533 — the versions a handler READ, kept so a Platform transaction body can start again from them. The executor runs
/// a body again after a transient write conflict. A write the aborted attempt made was never stored, but the store had
/// already moved the entity in hand to the next version — without this, the second attempt would hand the store a version
/// nobody stored and refuse its own legitimate write.
/// </summary>
internal sealed class ReadVersionSnapshot
{
    private readonly List<(BaseEntity Entity, int Version, DateTimeOffset? UpdatedAt)> _read = [];

    public void Remember(BaseEntity entity) => _read.Add((entity, entity.Version, entity.UpdatedAt));

    /// <summary>Called first thing in every attempt of the body.</summary>
    public void Restore()
    {
        foreach (var (entity, version, updatedAt) in _read)
        {
            entity.Version = version;
            entity.UpdatedAt = updatedAt;
        }
    }
}

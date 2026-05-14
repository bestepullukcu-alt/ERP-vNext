namespace Diten.Platform.Application.Features.InterfaceRegistry.Auditing;

public interface IInterfaceRegistryAuditSink
{
    Task EmitAsync(string eventName, IReadOnlyDictionary<string, string?> metadata, CancellationToken ct = default);
}

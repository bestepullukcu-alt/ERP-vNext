namespace Diten.Platform.Application.Features.InterfaceRegistry.Auditing;

public sealed class NullInterfaceRegistryAuditSink : IInterfaceRegistryAuditSink
{
    public Task EmitAsync(string eventName, IReadOnlyDictionary<string, string?> metadata, CancellationToken ct = default) =>
        Task.CompletedTask;
}

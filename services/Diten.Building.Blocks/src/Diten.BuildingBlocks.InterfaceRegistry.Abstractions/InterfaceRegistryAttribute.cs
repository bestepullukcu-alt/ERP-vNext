namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class InterfaceRegistryAttribute : Attribute
{
    public InterfaceRegistryAttribute(string code, string ownerModuleCode, string version)
    {
        Code = code;
        OwnerModuleCode = ownerModuleCode;
        Version = version;
    }

    public string Code { get; }
    public string OwnerModuleCode { get; }
    public string Version { get; }
    public InterfaceStability Stability { get; init; } = InterfaceStability.Stable;
    public InterfaceVisibility Visibility { get; init; } = InterfaceVisibility.Platform;
    public InterfaceLifecycleStatus LifecycleStatus { get; init; } = InterfaceLifecycleStatus.Discovered;
}

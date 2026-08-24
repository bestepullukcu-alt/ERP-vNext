namespace Diten.Platform.API.Configuration;

public sealed class ModuleRegistrationCredentialOptions
{
    public const string SectionName = "ModuleRegistrationCredentials";

    public ModuleRegistrationServiceCredentialOptions Mdm { get; set; } = new();
}

public sealed class ModuleRegistrationServiceCredentialOptions
{
    public string Identifier { get; set; } = string.Empty;
    public string ActiveSecret { get; set; } = string.Empty;
    public string? PreviousSecret { get; set; }
    public DateTimeOffset? PreviousValidUntilUtc { get; set; }
    public bool IsRevoked { get; set; }
}

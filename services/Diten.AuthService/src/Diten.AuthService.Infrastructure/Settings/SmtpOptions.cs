namespace Diten.AuthService.Infrastructure.Settings;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool EnableSsl { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    // BL-454 — there is no sender-NAME setting: the name is Diten.BuildingBlocks.Email.EmailProduct.Name (platform
    // mail) or the tenant's, by the one sender-name rule. Two settings are how two services ended up with two names.
}

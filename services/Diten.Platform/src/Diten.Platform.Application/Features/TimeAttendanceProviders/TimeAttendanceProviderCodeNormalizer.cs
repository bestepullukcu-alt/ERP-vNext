namespace Diten.Platform.Application.Features.TimeAttendanceProviders;

public static class TimeAttendanceProviderCodeNormalizer
{
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}

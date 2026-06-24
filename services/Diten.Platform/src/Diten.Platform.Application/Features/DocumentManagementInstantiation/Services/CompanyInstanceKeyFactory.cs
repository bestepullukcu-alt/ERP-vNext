namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;

public sealed class CompanyInstanceKeyFactory
{
    public string Create(Guid tenantId, Guid companyId, Guid baselineReleaseId, string canonicalId, string? instanceToken)
    {
        var key = $"{tenantId:D}|{companyId:D}|{baselineReleaseId:D}|{canonicalId}";
        return string.IsNullOrWhiteSpace(instanceToken) ? key : $"{key}|{instanceToken.Trim()}";
    }
}

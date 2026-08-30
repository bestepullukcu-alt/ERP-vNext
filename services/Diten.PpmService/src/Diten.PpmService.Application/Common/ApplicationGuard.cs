namespace Diten.PpmService.Application.Common;

internal static class ApplicationGuard
{
    internal static bool InvalidContext(ITenantContext tenant, ICurrentActorContext actor) => tenant.TenantId == Guid.Empty || actor.ActorId == Guid.Empty;
    internal static string NormalizeCode(string code) => code.Trim().Normalize(System.Text.NormalizationForm.FormC);
}

namespace Diten.InventoryService.Application.Features.WavePlanning.Ports;

/// <summary>What the user directory says about the user a wave names as responsible (pack §4.1 <c>ResponsibleUserId</c>, §12).</summary>
public enum WaveResponsibleCheck
{
    /// <summary>An active user of the caller's tenant.</summary>
    Valid,

    /// <summary>Not a user of the tenant, or not active → 422 <c>WAVE_RESPONSIBLE_INVALID</c>.</summary>
    Invalid,

    /// <summary>The directory could not answer → 503 <c>WAVE_RESPONSIBLE_DIRECTORY_UNAVAILABLE</c>, nothing written.</summary>
    Unavailable
}

/// <summary>
/// MOD-0180's own user-directory port (pack §4.1: "0178 <c>IWarehouseAssigneeDirectory</c> emsali; 0180 kendi portunu açar"). The production
/// adapter asks the same AUTH <c>lookup-validation</c> through the existing MOD-0178 HTTP client (called, not changed).
/// </summary>
public interface IWaveResponsibleDirectory
{
    Task<WaveResponsibleCheck> CheckAsync(Guid userId, CancellationToken cancellationToken = default);
}

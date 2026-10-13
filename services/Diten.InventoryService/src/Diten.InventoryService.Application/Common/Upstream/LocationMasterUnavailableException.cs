namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>LOCATION could not answer → 503 <c>LOCATION_MASTER_UNAVAILABLE</c>, nothing written.</summary>
public sealed class LocationMasterUnavailableException : Exception
{
    public LocationMasterUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

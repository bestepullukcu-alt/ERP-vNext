namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// MOD-0173-FU03 §8.3 step 5: aborts the posting transaction when the reservation the movement named by <c>reservationId</c> refuses it
/// (RESERVATION_NOT_FOUND / _NOT_ACTIVE / _MISMATCH / _QUANTITY_EXCEEDED). Carries the contract code, message and details.
/// </summary>
internal sealed class ReservationRefusedException : Exception
{
    public ReservationRefusedException(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }

    public IReadOnlyDictionary<string, object?>? Details { get; }
}

namespace Diten.Platform.Application.Contracts.Audit;

/// <summary>
/// Marks a command whose success audit intent is written by its handler in the same transaction as business state.
/// </summary>
public interface ITransactionOwnedAuditCommand;

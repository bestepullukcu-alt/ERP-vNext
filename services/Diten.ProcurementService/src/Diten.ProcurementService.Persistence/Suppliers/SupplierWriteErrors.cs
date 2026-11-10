using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Suppliers;

/// <summary>Recognizes a unique-index refusal (E11000) whichever driver exception carries it, and names the index.</summary>
internal static class SupplierWriteErrors
{
    public static bool IsDuplicateKey(Exception ex, out string message)
    {
        switch (ex)
        {
            case MongoWriteException w when w.WriteError?.Category == ServerErrorCategory.DuplicateKey:
                message = w.WriteError.Message ?? string.Empty;
                return true;
            case MongoBulkWriteException b when b.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey):
                message = string.Join(" ", b.WriteErrors.Select(e => e.Message));
                return true;
            case MongoCommandException c when c.Code == 11000:
                message = c.Message;
                return true;
            default:
                message = string.Empty;
                return false;
        }
    }
}

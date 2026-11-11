using MongoDB.Driver;

namespace Diten.SafetyStockService.Persistence;

public interface IPolicyAuditAppender
{
    Task AppendAsync(IClientSessionHandle session, PolicyAuditDocument record, CancellationToken cancellationToken);
}

public sealed class PolicyAuditAppender : IPolicyAuditAppender
{
    private readonly SafetyStockMongoContext _context;

    public PolicyAuditAppender(SafetyStockMongoContext context) => _context = context;

    public Task AppendAsync(IClientSessionHandle session, PolicyAuditDocument record, CancellationToken cancellationToken)
        => _context.Audit.InsertOneAsync(session, record, cancellationToken: cancellationToken);
}

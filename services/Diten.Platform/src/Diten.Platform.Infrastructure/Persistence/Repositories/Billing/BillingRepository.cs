using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums.Billing;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories.Billing;

public sealed class BillingRepository : IBillingRepository
{
    private const string InvoiceCounterName = "Invoice";
    private readonly IMongoClient _mongoClient;
    private readonly ITenantContext _tenantContext;
    private readonly IMongoCollection<BillingPlan> _plans;
    private readonly IMongoCollection<BillingInvoice> _invoices;
    private readonly IMongoCollection<BillingInvoiceCounter> _counters;
    private readonly IMongoCollection<PaymentRecord> _payments;
    private readonly IMongoCollection<RefundRecord> _refunds;
    private readonly IMongoCollection<BillingActivityEvent> _events;

    public BillingRepository(IPlatformDbContext dbContext, IMongoClient mongoClient, ITenantContext tenantContext)
    {
        _mongoClient = mongoClient;
        _tenantContext = tenantContext;
        _plans = dbContext.GetCollection<BillingPlan>("billing_plans");
        _invoices = dbContext.GetCollection<BillingInvoice>("billing_invoices");
        _counters = dbContext.GetCollection<BillingInvoiceCounter>("billing_invoice_counters");
        _payments = dbContext.GetCollection<PaymentRecord>("billing_payments");
        _refunds = dbContext.GetCollection<RefundRecord>("billing_refunds");
        _events = dbContext.GetCollection<BillingActivityEvent>("billing_activity_events");
    }

    public async Task<BillingPlan> CreatePlanAsync(BillingPlan plan, CancellationToken ct = default)
    {
        await _plans.InsertOneAsync(plan, cancellationToken: ct);
        await InsertEventAsync(BillingActivityEventType.PlanCreated, null, null, null, null, plan.CreatedBy, ct);
        return plan;
    }

    public async Task<BillingPlan?> GetPlanAsync(Guid id, CancellationToken ct = default) =>
        await _plans.Find(TenantFilter<BillingPlan>() & Builders<BillingPlan>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<BillingPlan>> GetPlansAsync(CancellationToken ct = default) =>
        await _plans.Find(TenantFilter<BillingPlan>())
            .SortBy(x => x.PlanCode)
            .ThenByDescending(x => x.PlanVersion)
            .ToListAsync(ct);

    public async Task<BillingPlan?> GetLatestPlanVersionAsync(string planCode, CancellationToken ct = default) =>
        await _plans.Find(TenantFilter<BillingPlan>() & Builders<BillingPlan>.Filter.Eq(x => x.PlanCode, planCode))
            .SortByDescending(x => x.PlanVersion)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> IsPlanUsedByInvoiceAsync(Guid planId, int planVersion, CancellationToken ct = default) =>
        await _invoices.Find(TenantFilter<BillingInvoice>()
            & Builders<BillingInvoice>.Filter.Eq(x => x.BillingPlanId, planId)
            & Builders<BillingInvoice>.Filter.Eq(x => x.BillingPlanVersion, planVersion))
            .AnyAsync(ct);

    public async Task<bool> ActivatePlanAsync(Guid planId, CancellationToken ct = default)
    {
        var update = Builders<BillingPlan>.Update
            .Set(x => x.Status, BillingPlanStatus.Active)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _plans.UpdateOneAsync(
            TenantFilter<BillingPlan>()
            & Builders<BillingPlan>.Filter.Eq(x => x.Id, planId)
            & Builders<BillingPlan>.Filter.Eq(x => x.Status, BillingPlanStatus.Draft),
            update,
            cancellationToken: ct);

        if (result.ModifiedCount > 0)
        {
            await InsertEventAsync(BillingActivityEventType.PlanActivated, null, null, null, null, string.Empty, ct);
        }

        return result.ModifiedCount > 0;
    }

    public async Task<BillingInvoice> CreateInvoiceAsync(BillingInvoice invoice, CancellationToken ct = default)
    {
        await _invoices.InsertOneAsync(invoice, cancellationToken: ct);
        await InsertEventAsync(BillingActivityEventType.InvoiceCreated, invoice.Id, invoice.InvoiceNumber, null, null, invoice.CreatedBy, ct);
        return invoice;
    }

    public async Task<BillingInvoice?> GetInvoiceAsync(Guid id, CancellationToken ct = default) =>
        await _invoices.Find(TenantFilter<BillingInvoice>() & Builders<BillingInvoice>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<BillingInvoice>> GetInvoicesAsync(CancellationToken ct = default) =>
        await _invoices.Find(TenantFilter<BillingInvoice>())
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> UpdateDraftInvoiceAsync(BillingInvoice invoice, CancellationToken ct = default)
    {
        var result = await _invoices.ReplaceOneAsync(
            TenantFilter<BillingInvoice>()
            & Builders<BillingInvoice>.Filter.Eq(x => x.Id, invoice.Id)
            & Builders<BillingInvoice>.Filter.Eq(x => x.Status, InvoiceStatus.Draft),
            invoice,
            cancellationToken: ct);

        if (result.ModifiedCount > 0)
        {
            await InsertEventAsync(BillingActivityEventType.InvoiceUpdated, invoice.Id, invoice.InvoiceNumber, null, null, invoice.UpdatedBy ?? string.Empty, ct);
        }

        return result.ModifiedCount > 0;
    }

    public async Task<(bool Succeeded, BillingInvoice? Invoice)> IssueInvoiceAsync(Guid invoiceId, string actorUserId, CancellationToken ct = default)
    {
        using var session = await _mongoClient.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();

        try
        {
            var now = DateTimeOffset.UtcNow;
            var year = now.Year;
            var counter = await _counters.FindOneAndUpdateAsync(
                session,
                TenantFilter<BillingInvoiceCounter>()
                & Builders<BillingInvoiceCounter>.Filter.Eq(x => x.Year, year)
                & Builders<BillingInvoiceCounter>.Filter.Eq(x => x.CounterName, InvoiceCounterName),
                Builders<BillingInvoiceCounter>.Update
                    .SetOnInsert(x => x.TenantId, _tenantContext.TenantId)
                    .SetOnInsert(x => x.Year, year)
                    .SetOnInsert(x => x.CounterName, InvoiceCounterName)
                    .SetOnInsert(x => x.CreatedBy, actorUserId)
                    .Inc(x => x.NextValue, 1),
                new FindOneAndUpdateOptions<BillingInvoiceCounter>
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.After
                },
                ct);

            var invoiceNumber = $"INV-{year}-{counter.NextValue:000000}";
            var update = Builders<BillingInvoice>.Update
                .Set(x => x.InvoiceNumber, invoiceNumber)
                .Set(x => x.Status, InvoiceStatus.Issued)
                .Set(x => x.IssuedAt, now)
                .Set(x => x.UpdatedAt, now)
                .Set(x => x.UpdatedBy, actorUserId);

            var result = await _invoices.UpdateOneAsync(
                session,
                TenantFilter<BillingInvoice>()
                & Builders<BillingInvoice>.Filter.Eq(x => x.Id, invoiceId)
                & Builders<BillingInvoice>.Filter.Eq(x => x.Status, InvoiceStatus.Draft)
                & Builders<BillingInvoice>.Filter.Eq(x => x.InvoiceNumber, null),
                update,
                cancellationToken: ct);

            if (result.ModifiedCount == 0)
            {
                await session.AbortTransactionAsync(ct);
                return (false, null);
            }

            var invoice = await _invoices.Find(session, TenantFilter<BillingInvoice>() & Builders<BillingInvoice>.Filter.Eq(x => x.Id, invoiceId)).FirstOrDefaultAsync(ct);
            await _events.InsertOneAsync(session, BuildEvent(BillingActivityEventType.InvoiceIssued, invoiceId, invoiceNumber, null, null, actorUserId), cancellationToken: ct);
            await session.CommitTransactionAsync(ct);
            return (true, invoice);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    public async Task<(bool Succeeded, BillingInvoice? Invoice)> CancelInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var update = Builders<BillingInvoice>.Update
            .Set(x => x.Status, InvoiceStatus.Cancelled)
            .Set(x => x.CancelledAt, now)
            .Set(x => x.UpdatedAt, now);

        var result = await _invoices.UpdateOneAsync(
            TenantFilter<BillingInvoice>()
            & Builders<BillingInvoice>.Filter.Eq(x => x.Id, invoiceId)
            & Builders<BillingInvoice>.Filter.Eq(x => x.Status, InvoiceStatus.Issued)
            & Builders<BillingInvoice>.Filter.Eq(x => x.PaidAmount, 0),
            update,
            cancellationToken: ct);

        if (result.ModifiedCount == 0)
        {
            return (false, null);
        }

        var invoice = await GetInvoiceAsync(invoiceId, ct);
        await InsertEventAsync(BillingActivityEventType.InvoiceCancelled, invoiceId, invoice?.InvoiceNumber, null, null, string.Empty, ct);
        return (true, invoice);
    }

    public async Task<PaymentRecord?> GetPaymentByIdempotencyKeyAsync(Guid invoiceId, string idempotencyKey, CancellationToken ct = default) =>
        await _payments.Find(TenantFilter<PaymentRecord>()
            & Builders<PaymentRecord>.Filter.Eq(x => x.InvoiceId, invoiceId)
            & Builders<PaymentRecord>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(ct);

    public async Task<(bool Succeeded, PaymentRecord? Payment, BillingInvoice? Invoice)> ApplyPaymentAsync(Guid invoiceId, PaymentRecord payment, CancellationToken ct = default)
    {
        using var session = await _mongoClient.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();

        try
        {
            var now = DateTimeOffset.UtcNow;
            var guardedUpdate = await _invoices.UpdateOneAsync(
                session,
                TenantFilter<BillingInvoice>()
                & Builders<BillingInvoice>.Filter.Eq(x => x.Id, invoiceId)
                & Builders<BillingInvoice>.Filter.Eq(x => x.Currency, payment.Currency)
                & Builders<BillingInvoice>.Filter.In(x => x.Status, new[] { InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid })
                & Builders<BillingInvoice>.Filter.Gte(x => x.BalanceDue, payment.AppliedAmount),
                Builders<BillingInvoice>.Update
                    .Inc(x => x.PaidAmount, payment.AppliedAmount)
                    .Inc(x => x.BalanceDue, -payment.AppliedAmount)
                    .Set(x => x.UpdatedAt, now)
                    .Set(x => x.UpdatedBy, payment.CreatedBy),
                cancellationToken: ct);

            if (guardedUpdate.ModifiedCount == 0)
            {
                await session.AbortTransactionAsync(ct);
                return (false, null, null);
            }

            try
            {
                await _payments.InsertOneAsync(session, payment, cancellationToken: ct);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                await session.AbortTransactionAsync(ct);
                var existing = await GetPaymentByIdempotencyKeyAsync(invoiceId, payment.IdempotencyKey, ct);
                return existing is null ? (false, null, null) : (true, existing, await GetInvoiceAsync(invoiceId, ct));
            }

            var invoice = await _invoices.Find(session, TenantFilter<BillingInvoice>() & Builders<BillingInvoice>.Filter.Eq(x => x.Id, invoiceId)).FirstOrDefaultAsync(ct);
            if (invoice is null)
            {
                await session.AbortTransactionAsync(ct);
                return (false, null, null);
            }

            var nextStatus = invoice.BalanceDue == 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            await _invoices.UpdateOneAsync(
                session,
                TenantFilter<BillingInvoice>() & Builders<BillingInvoice>.Filter.Eq(x => x.Id, invoiceId),
                Builders<BillingInvoice>.Update.Set(x => x.Status, nextStatus),
                cancellationToken: ct);

            invoice.Status = nextStatus;
            await _events.InsertOneAsync(session, BuildEvent(BillingActivityEventType.PaymentApplied, invoiceId, invoice.InvoiceNumber, payment.Id, null, payment.CreatedBy), cancellationToken: ct);
            await session.CommitTransactionAsync(ct);
            return (true, payment, invoice);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    public async Task<(bool Succeeded, RefundRecord? Refund, BillingInvoice? Invoice)> CreateRefundAsync(Guid paymentRecordId, RefundRecord refund, CancellationToken ct = default)
    {
        using var session = await _mongoClient.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();

        try
        {
            var payment = await _payments.Find(session, TenantFilter<PaymentRecord>() & Builders<PaymentRecord>.Filter.Eq(x => x.Id, paymentRecordId)).FirstOrDefaultAsync(ct);
            if (payment is null || payment.Currency != refund.Currency)
            {
                await session.AbortTransactionAsync(ct);
                return (false, null, null);
            }

            var paymentUpdate = await _payments.UpdateOneAsync(
                session,
                TenantFilter<PaymentRecord>()
                & Builders<PaymentRecord>.Filter.Eq(x => x.Id, paymentRecordId)
                & Builders<PaymentRecord>.Filter.Eq(x => x.Currency, refund.Currency)
                & Builders<PaymentRecord>.Filter.Eq(x => x.RefundedAmount, payment.RefundedAmount)
                & Builders<PaymentRecord>.Filter.Gte(x => x.AppliedAmount, payment.RefundedAmount + refund.RefundAmount),
                Builders<PaymentRecord>.Update
                    .Inc(x => x.RefundedAmount, refund.RefundAmount)
                    .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                    .Set(x => x.UpdatedBy, refund.CreatedBy),
                cancellationToken: ct);

            if (paymentUpdate.ModifiedCount == 0)
            {
                await session.AbortTransactionAsync(ct);
                return (false, null, null);
            }

            refund.InvoiceId = payment.InvoiceId;
            await _refunds.InsertOneAsync(session, refund, cancellationToken: ct);

            var invoiceUpdate = await _invoices.UpdateOneAsync(
                session,
                TenantFilter<BillingInvoice>()
                & Builders<BillingInvoice>.Filter.Eq(x => x.Id, payment.InvoiceId)
                & Builders<BillingInvoice>.Filter.Eq(x => x.Currency, refund.Currency)
                & Builders<BillingInvoice>.Filter.Ne(x => x.Status, InvoiceStatus.Cancelled),
                Builders<BillingInvoice>.Update
                    .Inc(x => x.PaidAmount, -refund.RefundAmount)
                    .Inc(x => x.BalanceDue, refund.RefundAmount)
                    .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                    .Set(x => x.UpdatedBy, refund.CreatedBy),
                cancellationToken: ct);

            if (invoiceUpdate.ModifiedCount == 0)
            {
                await session.AbortTransactionAsync(ct);
                return (false, null, null);
            }

            var invoice = await _invoices.Find(session, TenantFilter<BillingInvoice>() & Builders<BillingInvoice>.Filter.Eq(x => x.Id, payment.InvoiceId)).FirstOrDefaultAsync(ct);
            if (invoice is null)
            {
                await session.AbortTransactionAsync(ct);
                return (false, null, null);
            }

            var nextInvoiceStatus = invoice.PaidAmount == 0 ? InvoiceStatus.Issued : InvoiceStatus.PartiallyPaid;
            await _invoices.UpdateOneAsync(
                session,
                TenantFilter<BillingInvoice>() & Builders<BillingInvoice>.Filter.Eq(x => x.Id, invoice.Id),
                Builders<BillingInvoice>.Update.Set(x => x.Status, nextInvoiceStatus),
                cancellationToken: ct);
            invoice.Status = nextInvoiceStatus;

            var updatedPayment = await _payments.Find(session, TenantFilter<PaymentRecord>() & Builders<PaymentRecord>.Filter.Eq(x => x.Id, paymentRecordId)).FirstOrDefaultAsync(ct);
            if (updatedPayment is not null)
            {
                var nextPaymentStatus = updatedPayment.RefundedAmount == updatedPayment.AppliedAmount
                    ? PaymentStatus.Refunded
                    : PaymentStatus.PartiallyRefunded;
                await _payments.UpdateOneAsync(
                    session,
                    TenantFilter<PaymentRecord>() & Builders<PaymentRecord>.Filter.Eq(x => x.Id, paymentRecordId),
                    Builders<PaymentRecord>.Update.Set(x => x.Status, nextPaymentStatus),
                    cancellationToken: ct);
            }

            await _events.InsertOneAsync(session, BuildEvent(BillingActivityEventType.RefundCreated, invoice.Id, invoice.InvoiceNumber, paymentRecordId, refund.Id, refund.CreatedBy), cancellationToken: ct);
            await session.CommitTransactionAsync(ct);
            return (true, refund, invoice);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    private FilterDefinition<TEntity> TenantFilter<TEntity>() where TEntity : Diten.Platform.Common.Persistence.TenantScopedEntity =>
        Builders<TEntity>.Filter.Eq(x => x.TenantId, _tenantContext.TenantId)
        & Builders<TEntity>.Filter.Eq(x => x.IsDeleted, false);

    private async Task InsertEventAsync(
        BillingActivityEventType eventType,
        Guid? invoiceId,
        string? invoiceNumber,
        Guid? paymentRecordId,
        Guid? refundRecordId,
        string actorUserId,
        CancellationToken ct)
    {
        await _events.InsertOneAsync(BuildEvent(eventType, invoiceId, invoiceNumber, paymentRecordId, refundRecordId, actorUserId), cancellationToken: ct);
    }

    private BillingActivityEvent BuildEvent(
        BillingActivityEventType eventType,
        Guid? invoiceId,
        string? invoiceNumber,
        Guid? paymentRecordId,
        Guid? refundRecordId,
        string actorUserId) =>
        new()
        {
            TenantId = _tenantContext.TenantId,
            CreatedBy = actorUserId,
            EventType = eventType,
            InvoiceId = invoiceId,
            InvoiceNumber = invoiceNumber,
            PaymentRecordId = paymentRecordId,
            RefundRecordId = refundRecordId,
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserId = actorUserId
        };
}

using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class ProductLegalEntityScopePolicy : EntityBase, IAuditIntentAggregate
{
    public const int MaximumLegalEntityIdsPerSnapshot = 200;
    public const int MaximumPeriodsPerPolicy = 100;
    public const int MaximumSerializedBsonBytes = 1_048_576;

    public Guid GlobalProductId { get; set; }
    public Guid CreationCommandId { get; set; }
    public List<ProductLegalEntityScopePeriod> ScopePeriods { get; set; } = [];
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];

    public static ProductLegalEntityScopePolicy Create(
        Guid tenantId,
        Guid globalProductId,
        Guid creationCommandId,
        ProductLegalEntityScopeMode mode,
        IEnumerable<Guid> legalEntityIds,
        Guid actorId,
        DateTimeOffset serverNowUtc)
    {
        EnsureIdentity(tenantId, nameof(tenantId));
        EnsureIdentity(globalProductId, nameof(globalProductId));
        EnsureIdentity(creationCommandId, nameof(creationCommandId));
        EnsureIdentity(actorId, nameof(actorId));
        EnsureUtc(serverNowUtc, nameof(serverNowUtc));

        var policy = new ProductLegalEntityScopePolicy
        {
            TenantId = tenantId,
            GlobalProductId = globalProductId,
            CreationCommandId = creationCommandId,
            CreatedAt = serverNowUtc,
            Version = 0,
            ScopePeriods =
            [
                ProductLegalEntityScopePeriod.Create(
                    mode,
                    NormalizeLegalEntityIds(mode, legalEntityIds),
                    serverNowUtc,
                    creationCommandId,
                    actorId)
            ]
        };
        policy.EnsureValid(serverNowUtc);
        return policy;
    }

    public ProductLegalEntityScopePeriod? GetEffectivePeriod(DateTimeOffset serverNowUtc)
    {
        EnsureUtc(serverNowUtc, nameof(serverNowUtc));
        var effective = ScopePeriods
            .Where(period => period.EffectiveFromUtc <= serverNowUtc
                             && (!period.EffectiveToUtc.HasValue || serverNowUtc < period.EffectiveToUtc.Value))
            .ToList();
        return effective.Count switch
        {
            0 => null,
            1 => effective[0],
            _ => throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_MULTIPLE_CURRENT_PERIODS")
        };
    }

    public void ReplaceCurrent(
        int expectedVersion,
        Guid commandId,
        ProductLegalEntityScopeMode mode,
        IEnumerable<Guid> legalEntityIds,
        Guid actorId,
        DateTimeOffset serverNowUtc)
    {
        EnsureExpectedVersion(expectedVersion);
        EnsureIdentity(commandId, nameof(commandId));
        EnsureIdentity(actorId, nameof(actorId));
        EnsureUtc(serverNowUtc, nameof(serverNowUtc));
        EnsureCommandIsNew(commandId);
        if (ScopePeriods.Count >= MaximumPeriodsPerPolicy)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_PERIOD_LIMIT_EXCEEDED");
        }

        var legalEntities = NormalizeLegalEntityIds(mode, legalEntityIds);
        var current = GetEffectivePeriod(serverNowUtc)
                      ?? throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_CURRENT_PERIOD_REQUIRED");
        if (current.EffectiveFromUtc >= serverNowUtc)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_SERVER_TIME_ORDER_INVALID");
        }

        current.EffectiveToUtc = serverNowUtc;
        current.EndCommandId = commandId;
        current.EndedByActorId = actorId;
        current.EndedAtUtc = serverNowUtc;
        ScopePeriods.Add(ProductLegalEntityScopePeriod.Create(
            mode,
            legalEntities,
            serverNowUtc,
            commandId,
            actorId));
        ScopePeriods = ScopePeriods
            .OrderBy(period => period.EffectiveFromUtc)
            .ThenBy(period => period.PeriodId)
            .ToList();
        UpdatedAt = serverNowUtc;
        Version++;
        EnsureValid(serverNowUtc);
    }

    public void EndCurrent(
        int expectedVersion,
        Guid commandId,
        Guid actorId,
        DateTimeOffset serverNowUtc)
    {
        EnsureExpectedVersion(expectedVersion);
        EnsureIdentity(commandId, nameof(commandId));
        EnsureIdentity(actorId, nameof(actorId));
        EnsureUtc(serverNowUtc, nameof(serverNowUtc));
        EnsureCommandIsNew(commandId);
        var current = GetEffectivePeriod(serverNowUtc)
                      ?? throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_CURRENT_PERIOD_REQUIRED");
        if (current.EffectiveFromUtc >= serverNowUtc)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_SERVER_TIME_ORDER_INVALID");
        }

        current.EffectiveToUtc = serverNowUtc;
        current.EndCommandId = commandId;
        current.EndedByActorId = actorId;
        current.EndedAtUtc = serverNowUtc;
        UpdatedAt = serverNowUtc;
        Version++;
        EnsureValid(serverNowUtc);
    }

    public void EnsureValid(DateTimeOffset serverNowUtc)
    {
        EnsureUtc(serverNowUtc, nameof(serverNowUtc));
        EnsureIdentity(TenantId, nameof(TenantId));
        EnsureIdentity(GlobalProductId, nameof(GlobalProductId));
        EnsureIdentity(CreationCommandId, nameof(CreationCommandId));
        if (Version < 0 || ScopePeriods.Count > MaximumPeriodsPerPolicy)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_POLICY_LIMIT_INVALID");
        }
        if (ScopePeriods.Select(period => period.PeriodId).Distinct().Count() != ScopePeriods.Count
            || ScopePeriods.Select(period => period.CommandId).Distinct().Count() != ScopePeriods.Count
            || ScopePeriods.Where(period => period.EndCommandId.HasValue)
                .Select(period => period.EndCommandId!.Value).Distinct().Count()
               != ScopePeriods.Count(period => period.EndCommandId.HasValue))
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_PERIOD_IDENTITY_DUPLICATE");
        }

        var ordered = ScopePeriods
            .OrderBy(period => period.EffectiveFromUtc)
            .ThenBy(period => period.PeriodId)
            .ToList();
        if (!ordered.SequenceEqual(ScopePeriods))
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_PERIODS_NOT_CANONICAL");
        }
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].EnsureValid(serverNowUtc);
            if (ordered[index].EndCommandId.HasValue
                && ordered.Any(candidate => candidate.CommandId == ordered[index].EndCommandId)
                && (index + 1 >= ordered.Count
                    || ordered[index + 1].CommandId != ordered[index].EndCommandId
                    || ordered[index].EffectiveToUtc != ordered[index + 1].EffectiveFromUtc))
            {
                throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_END_COMMAND_LINK_INVALID");
            }
            if (index > 0)
            {
                var previous = ordered[index - 1];
                if (!previous.EffectiveToUtc.HasValue
                    || previous.EffectiveToUtc.Value > ordered[index].EffectiveFromUtc)
                {
                    throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_PERIOD_OVERLAP");
                }
            }
        }

        _ = GetEffectivePeriod(serverNowUtc);
    }

    public static IReadOnlyList<Guid> NormalizeLegalEntityIds(
        ProductLegalEntityScopeMode mode,
        IEnumerable<Guid> legalEntityIds)
    {
        ArgumentNullException.ThrowIfNull(legalEntityIds);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var supplied = legalEntityIds.ToList();
        if (supplied.Count > MaximumLegalEntityIdsPerSnapshot)
        {
            throw new ArgumentOutOfRangeException(
                nameof(legalEntityIds),
                "PRODUCT_LEGAL_ENTITY_SCOPE_ID_LIMIT_EXCEEDED");
        }
        if (supplied.Any(id => id == Guid.Empty) || supplied.Distinct().Count() != supplied.Count)
        {
            throw new ArgumentException(
                "PRODUCT_LEGAL_ENTITY_SCOPE_IDS_MUST_BE_NONEMPTY_UNIQUE",
                nameof(legalEntityIds));
        }

        var normalized = supplied
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (mode == ProductLegalEntityScopeMode.GroupWide && normalized.Length != 0)
        {
            throw new ArgumentException(
                "PRODUCT_LEGAL_ENTITY_SCOPE_GROUP_WIDE_IDS_FORBIDDEN",
                nameof(legalEntityIds));
        }
        if (mode == ProductLegalEntityScopeMode.Scoped && normalized.Length == 0)
        {
            throw new ArgumentException(
                "PRODUCT_LEGAL_ENTITY_SCOPE_SCOPED_IDS_REQUIRED",
                nameof(legalEntityIds));
        }

        return normalized;
    }

    public static void EnsureSerializedBsonSizeWithinLimit(int completeSerializedBsonBytes)
    {
        if (completeSerializedBsonBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completeSerializedBsonBytes));
        }
        if (completeSerializedBsonBytes > MaximumSerializedBsonBytes)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_BSON_LIMIT_EXCEEDED");
        }
    }

    private void EnsureExpectedVersion(int expectedVersion)
    {
        if (expectedVersion < 0 || Version != expectedVersion)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_VERSION_CONFLICT");
        }
    }

    private void EnsureCommandIsNew(Guid commandId)
    {
        if (ScopePeriods.Any(period => period.CommandId == commandId || period.EndCommandId == commandId))
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_COMMAND_ALREADY_APPLIED");
        }
    }

    private static void EnsureIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identity must be non-empty.", parameterName);
        }
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Server time must be UTC.", parameterName);
        }
    }
}

public sealed class ProductLegalEntityScopePeriod
{
    public Guid PeriodId { get; set; }
    public ProductLegalEntityScopeMode Mode { get; set; }
    public List<Guid> LegalEntityIds { get; set; } = [];
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public Guid CommandId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? EndCommandId { get; set; }
    public Guid? EndedByActorId { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }

    internal static ProductLegalEntityScopePeriod Create(
        ProductLegalEntityScopeMode mode,
        IReadOnlyList<Guid> legalEntityIds,
        DateTimeOffset serverNowUtc,
        Guid commandId,
        Guid actorId) => new()
        {
            PeriodId = Guid.NewGuid(),
            Mode = mode,
            LegalEntityIds = legalEntityIds.ToList(),
            EffectiveFromUtc = serverNowUtc,
            CommandId = commandId,
            ActorId = actorId,
            CreatedAtUtc = serverNowUtc
        };

    internal void EnsureValid(DateTimeOffset serverNowUtc)
    {
        if (PeriodId == Guid.Empty || CommandId == Guid.Empty || ActorId == Guid.Empty)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_PERIOD_IDENTITY_INVALID");
        }
        var hasAnyEndFact = EndCommandId.HasValue || EndedByActorId.HasValue || EndedAtUtc.HasValue;
        var hasCompleteEndFact = EndCommandId.HasValue && EndedByActorId.HasValue && EndedAtUtc.HasValue;
        if (EffectiveFromUtc.Offset != TimeSpan.Zero
            || CreatedAtUtc.Offset != TimeSpan.Zero
            || EffectiveFromUtc > serverNowUtc
            || CreatedAtUtc > serverNowUtc
            || (EffectiveToUtc.HasValue
                && (EffectiveToUtc.Value.Offset != TimeSpan.Zero
                    || EffectiveToUtc.Value <= EffectiveFromUtc
                    || EffectiveToUtc.Value > serverNowUtc))
            || hasAnyEndFact != hasCompleteEndFact
            || EffectiveToUtc.HasValue != hasCompleteEndFact
            || hasCompleteEndFact
               && (EndCommandId == Guid.Empty
                   || EndedByActorId == Guid.Empty
                   || EndedAtUtc!.Value.Offset != TimeSpan.Zero
                   || EndedAtUtc.Value > serverNowUtc
                   || EndedAtUtc.Value != EffectiveToUtc))
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_PERIOD_TIME_INVALID");
        }

        var normalized = ProductLegalEntityScopePolicy.NormalizeLegalEntityIds(Mode, LegalEntityIds);
        if (!normalized.SequenceEqual(LegalEntityIds))
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_IDS_NOT_CANONICAL");
        }
    }
}

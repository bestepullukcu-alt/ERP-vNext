using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FinishedGoodDraftFoundationUnitTests
{
    [Fact]
    public void Create_contract_has_one_business_field_and_rejects_forbidden_or_unknown_fields()
    {
        var request = new ProductItemSkuMasterModels.CreateFinishedGoodDraftRequest
        {
            GskuId = Guid.NewGuid(),
            IdempotencyKey = "technical-command",
            UnmappedFields = new Dictionary<string, JsonElement>
            {
                ["LskuId"] = JsonDocument.Parse("\"forbidden\"").RootElement.Clone(),
                ["InventedField"] = JsonDocument.Parse("true").RootElement.Clone()
            }
        };

        var result = new CreateFinishedGoodDraftValidator().Validate(new CreateFinishedGoodDraftCommand(request));

        Assert.Contains(result.Errors, error => error.ErrorMessage == "FINISHED_GOOD_FIELD_FORBIDDEN");
        Assert.Contains(result.Errors, error => error.ErrorMessage == "UNKNOWN_WRITE_FIELD_FORBIDDEN");
        Assert.Equal(
            ["GskuId", "IdempotencyKey", "UnmappedFields"],
            typeof(ProductItemSkuMasterModels.CreateFinishedGoodDraftRequest)
                .GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray());
    }

    [Fact]
    public void Create_contract_rejects_empty_gsku_and_exposes_no_finished_good_update_contract()
    {
        var result = new CreateFinishedGoodDraftValidator().Validate(new CreateFinishedGoodDraftCommand(new()
        {
            GskuId = Guid.Empty,
            IdempotencyKey = "technical-command"
        }));

        Assert.Contains(result.Errors, error => error.ErrorMessage == "GSKU_ID_REQUIRED");
        Assert.DoesNotContain(
            typeof(Diten.MdmService.Domain.Repositories.IFinishedGoodRepository).GetMethods(),
            method => method.Name.Contains("Update", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("TenantId")]
    [InlineData("CanonicalCode")]
    [InlineData("CodeReservationId")]
    [InlineData("StewardLabel")]
    [InlineData("LskuId")]
    [InlineData("MarketSupplyAssignmentId")]
    [InlineData("MarketCode")]
    [InlineData("LegalEntityId")]
    [InlineData("MarketTradeName")]
    [InlineData("PackagingLevelCode")]
    [InlineData("SiteId")]
    [InlineData("ManufacturerId")]
    [InlineData("MarketingAuthorizationId")]
    [InlineData("RegisteredPresentationId")]
    [InlineData("ArtworkId")]
    [InlineData("Gtin")]
    [InlineData("BatchId")]
    [InlineData("CompositionId")]
    [InlineData("LifecycleStatus")]
    [InlineData("Version")]
    [InlineData("CreatedAt")]
    [InlineData("AuditIntents")]
    public void Every_forbidden_write_family_fails_closed(string field)
    {
        var result = new CreateFinishedGoodDraftValidator().Validate(new CreateFinishedGoodDraftCommand(new()
        {
            GskuId = Guid.NewGuid(),
            IdempotencyKey = "technical-command",
            UnmappedFields = new Dictionary<string, JsonElement>
            {
                [field] = JsonDocument.Parse("null").RootElement.Clone()
            }
        }));

        Assert.Contains(result.Errors, error => error.ErrorMessage == "FINISHED_GOOD_FIELD_FORBIDDEN");
    }

    [Fact]
    public void Query_bounds_match_the_existing_bounded_mdm_standard()
    {
        Assert.False(new GetFinishedGoodsValidator().Validate(new GetFinishedGoodsQuery
        {
            PageNumber = 0,
            PageSize = 101,
            Search = new string('X', 201)
        }).IsValid);
        Assert.False(new GetFinishedGoodGskuSelectorValidator().Validate(new GetFinishedGoodGskuSelectorQuery
        {
            PageNumber = 1_000_001,
            PageSize = 0,
            Search = new string('X', 201)
        }).IsValid);

        var listDefaults = new GetFinishedGoodsQuery();
        var selectorDefaults = new GetFinishedGoodGskuSelectorQuery();
        Assert.Equal(1, listDefaults.PageNumber);
        Assert.Equal(20, listDefaults.PageSize);
        Assert.Equal(1, selectorDefaults.PageNumber);
        Assert.Equal(20, selectorDefaults.PageSize);
    }

    [Fact]
    public void Finished_good_schema_has_exactly_one_gsku_link_and_append_only_audit_values()
    {
        var properties = typeof(FinishedGood).GetProperties().Select(property => property.Name).ToHashSet();

        Assert.Contains("GskuId", properties);
        Assert.DoesNotContain("LskuId", properties);
        Assert.DoesNotContain("StewardLabel", properties);
        Assert.DoesNotContain("MarketSupplyAssignmentId", properties);
        Assert.Equal(5, (int)AuditAggregateType.FinishedGood);
        Assert.Equal(9, (int)ProductAuditOperation.FinishedGoodDraftCreated);
    }

    [Fact]
    public void Repository_result_marks_only_explicit_write_outcomes_as_ambiguous()
    {
        var rejected = new FinishedGoodCreateResult(false, null, "FINISHED_GOOD_DUPLICATE_CONFLICT");
        var ambiguous = new FinishedGoodCreateResult(
            false,
            null,
            "FINISHED_GOOD_WRITE_OUTCOME_AMBIGUOUS",
            WriteOutcomeAmbiguous: true);

        Assert.False(rejected.WriteOutcomeAmbiguous);
        Assert.True(ambiguous.WriteOutcomeAmbiguous);
    }

    [Fact]
    public async Task Programming_contract_violation_propagates_instead_of_becoming_reconciliation_pending()
    {
        var handler = new CreateFinishedGoodDraftHandler(null!, null!, null!, null!, null!);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Unexpected_repository_exception_propagates_instead_of_becoming_reconciliation_pending()
    {
        var fixture = HandlerFixture.Create(_ => throw new NullReferenceException("programming defect"));

        var exception = await Assert.ThrowsAsync<NullReferenceException>(() => fixture.Handle());

        Assert.Equal("programming defect", exception.Message);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, fixture.Reservations.Current.BindingState);
        Assert.Equal(0, fixture.Reservations.ConfirmCalls);
    }

    [Fact]
    public async Task Operation_cancellation_from_repository_propagates_instead_of_becoming_reconciliation_pending()
    {
        var fixture = HandlerFixture.Create(_ => throw new OperationCanceledException("cancelled write"));

        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Handle());

        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, fixture.Reservations.Current.BindingState);
        Assert.Equal(0, fixture.Reservations.ConfirmCalls);
    }

    [Fact]
    public async Task Ambiguous_write_with_matching_persisted_facts_recovers_and_confirms_binding()
    {
        var fixture = HandlerFixture.Create(
            _ => new FinishedGoodCreateResult(
                false,
                null,
                "FINISHED_GOOD_WRITE_OUTCOME_AMBIGUOUS",
                WriteOutcomeAmbiguous: true),
            returnCapturedOnReread: true);

        var response = await fixture.Handle();

        Assert.True(response.IsSuccessful, string.Join(',', response.Errors));
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(fixture.FinishedGoods.Captured!.Id, response.Data!.FinishedGoodId);
        Assert.Equal(1, fixture.Reservations.ConfirmCalls);
        Assert.Equal(CodeReservationBindingState.Confirmed, fixture.Reservations.Current.BindingState);
    }

    [Fact]
    public async Task Ambiguous_write_without_matching_persisted_proof_requires_reconciliation()
    {
        var fixture = HandlerFixture.Create(_ => new FinishedGoodCreateResult(
            false,
            null,
            "FINISHED_GOOD_WRITE_OUTCOME_AMBIGUOUS",
            WriteOutcomeAmbiguous: true));

        var response = await fixture.Handle();

        Assert.False(response.IsSuccessful);
        Assert.Equal(202, response.StatusCode);
        Assert.Contains("FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED", response.Errors);
        Assert.Equal(0, fixture.Reservations.ConfirmCalls);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, fixture.Reservations.Current.BindingState);
    }

    private sealed class HandlerFixture
    {
        private readonly CreateFinishedGoodDraftHandler _handler;
        private readonly Guid _gskuId;

        private HandlerFixture(
            CreateFinishedGoodDraftHandler handler,
            Guid gskuId,
            TestFinishedGoodRepository finishedGoods,
            TestReservationRepository reservations)
        {
            _handler = handler;
            _gskuId = gskuId;
            FinishedGoods = finishedGoods;
            Reservations = reservations;
        }

        public TestFinishedGoodRepository FinishedGoods { get; }
        public TestReservationRepository Reservations { get; }

        public static HandlerFixture Create(
            Func<FinishedGood, FinishedGoodCreateResult> create,
            bool returnCapturedOnReread = false)
        {
            var tenantId = Guid.NewGuid();
            var gsku = new Gsku
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CanonicalCode = "GS-UNIT-0001",
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft
            };
            var finishedGoods = new TestFinishedGoodRepository(create, returnCapturedOnReread);
            var reservations = new TestReservationRepository(tenantId);
            var handler = new CreateFinishedGoodDraftHandler(
                reservations,
                finishedGoods,
                new TestGskuRepository(gsku),
                new TestTenantContext(tenantId),
                new TestActorContext());
            return new(handler, gsku.Id, finishedGoods, reservations);
        }

        public Task<Diten.Shared.Core.Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>> Handle()
            => _handler.Handle(new CreateFinishedGoodDraftCommand(new()
            {
                GskuId = _gskuId,
                IdempotencyKey = "unit-hardening-command"
            }), CancellationToken.None);
    }

    private sealed class TestFinishedGoodRepository(
        Func<FinishedGood, FinishedGoodCreateResult> create,
        bool returnCapturedOnReread) : IFinishedGoodRepository
    {
        public FinishedGood? Captured { get; private set; }

        public Task<FinishedGood?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<FinishedGood?>(null);

        public Task<FinishedGood?> GetByCreationCommandIdAsync(
            string creationCommandId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<FinishedGood?>(null);

        public Task<FinishedGood?> GetByReservationIdAsync(
            Guid reservationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(returnCapturedOnReread ? Captured : null);

        public Task<FinishedGoodPage> GetPageAsync(
            int pageNumber,
            int pageSize,
            string? canonicalCodeSearch,
            IReadOnlyCollection<Guid>? matchingGskuIds,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FinishedGoodCreateResult> CreateDraftAsync(
            FinishedGood finishedGood,
            CancellationToken cancellationToken = default)
        {
            Captured = finishedGood;
            return Task.FromResult(create(finishedGood));
        }
    }

    private sealed class TestReservationRepository(Guid tenantId) : ICodeReservationRepository
    {
        public CodeReservation Current { get; private set; } = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = CodeBearingEntityType.FinishedGood,
            ReservedCode = "FG-UNIT-0001",
            ReservationState = CodeReservationState.Reserved,
            BindingState = CodeReservationBindingState.None,
            Version = 0
        };

        public int ConfirmCalls { get; private set; }

        public Task<CodeReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<CodeReservation?>(Current.Id == id ? Current : null);

        public Task<CodeReservation> ReserveAsync(
            CodeBearingEntityType entityType,
            string idempotencyKey,
            string actorId,
            string correlationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Current);

        public Task<ReservationOperationResult> ConsumeForIdentityAsync(
            Guid reservationId,
            CodeBearingEntityType expectedEntityType,
            Guid identityId,
            int expectedVersion,
            string idempotencyKey,
            string actorId,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            Current.ReservationState = CodeReservationState.Consumed;
            Current.BindingState = CodeReservationBindingState.PendingIdentityWrite;
            Current.ConsumedEntityId = identityId;
            Current.Version++;
            return Task.FromResult(new ReservationOperationResult(true, Current));
        }

        public Task<ReservationOperationResult> ConfirmIdentityBindingAsync(
            Guid reservationId,
            Guid identityId,
            int expectedVersion,
            string idempotencyKey,
            string actorId,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            ConfirmCalls++;
            Current.BindingState = CodeReservationBindingState.Confirmed;
            Current.Version++;
            return Task.FromResult(new ReservationOperationResult(true, Current));
        }
    }

    private sealed class TestGskuRepository(Gsku gsku) : IGskuRepository
    {
        public Task<Gsku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Gsku?>(gsku.Id == id ? gsku : null);

        public Task<Gsku?> GetReferenceableByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<Gsku>> GetByIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Gsku>>(ids.Contains(gsku.Id) ? [gsku] : []);

        public Task<GskuPage> GetReferenceablePageAsync(
            int pageNumber,
            int pageSize,
            string? canonicalCodeSearch,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> FindIdsByCanonicalCodeAsync(
            string canonicalCodeSearch,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Gsku?> GetByCreationCommandIdAsync(
            string creationCommandId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<GskuCreateResult> CreateDraftAsync(Gsku value, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<GskuUpdateResult> UpdateDraftAsync(
            Gsku value,
            int expectedVersion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => true;
        public void SetTenant(Guid value) => TenantId = value;
    }

    private sealed class TestActorContext : IProductIdentityActorContext
    {
        public string ActorId => "finished-good-hardening-test";
    }
}

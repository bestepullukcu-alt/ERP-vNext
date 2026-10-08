using System.Text;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Claims;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Infrastructure.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class HistoryImportTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherActor = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ThirdActor = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid LegalEntity = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherLegalEntity = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid Sku = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private const string Header = "sourceRecordKey,skuId,skuLevel,warehouseId,occurredOn,quantity,uomId,recordKind,relatedSourceRecordKey";

    private static string Row(string key = "SHIP-1", string warehouse = "wh-01",
        string date = "2026-09-15", string quantity = "2", string kind = "Shipment",
        string related = "") =>
        $"{key},{Sku:D},Lsku,{warehouse},{date},{quantity},BOX,{kind},{related}";

    private static CreateDemandHistoryImportBatchCommand Command(string body,
        string key = "import-1", Guid? tenant = null, Guid? legalEntityHint = null) => new(
        tenant ?? Tenant, Actor, legalEntityHint ?? LegalEntity, "controlled-file", "history.csv",
        new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
        ["wh-01"], Encoding.UTF8.GetBytes(body), key);

    private static CreateDemandHistoryImportBatchHandler Handler(MemoryStore store,
        IHistoryImportScopeAuthority? authority = null, FakeReferences? references = null) =>
        new(authority ?? new FakeAuthority(true), references ?? new FakeReferences(),
            new CsvHistoryImportFileParser(), store);

    private static ReviewHistoryImportBatchCommand Review(Guid batchId,
        Guid? actor = null, ImportBatchReviewState decision = ImportBatchReviewState.Approved,
        string reason = "Checked source evidence", string key = "review-1",
        bool permission = true, Guid? tenant = null, Guid? legalEntityHint = null) =>
        new(tenant ?? Tenant, actor ?? OtherActor, batchId, legalEntityHint ?? LegalEntity,
            permission, decision, reason, key);

    [Fact]
    public async Task Create_ValidShipmentFile_PersistsLineageAndVisibleWarning()
    {
        var store = new MemoryStore();
        var result = await Handler(store).Handle(Command(Header + "\n" + Row()), default);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(1, result.Data!.RowCount);
        Assert.Equal(0, result.Data.QuarantineCount);
        Assert.False(result.Data.ApprovalBlocked);
        Assert.Equal(ImportBatchValidationState.ValidatedWithWarnings, result.Data.ValidationState);
        var batch = Assert.Single(store.Batches);
        Assert.Equal(Tenant, batch.TenantId);
        Assert.Equal(LegalEntity, batch.LegalEntityId);
        Assert.Equal(Actor, batch.UploadedByActorId);
        Assert.Equal("File", batch.SourceChannel);
        Assert.Equal("controlled-file", batch.SourceSystem);
        Assert.Equal("history.csv", batch.SourceFileName);
        Assert.Equal(64, batch.SourceFileSha256.Length);
        Assert.Equal(new DateOnly(2026, 9, 1), batch.ScopeFrom);
        Assert.Equal("wh-01", Assert.Single(batch.WarehouseScope));
        Assert.Equal(9, batch.FieldScope.Count);
        var row = Assert.Single(batch.Rows);
        Assert.Equal(2m, row.OriginalQuantity);
        Assert.Equal("BOX", row.OriginalUomId);
        Assert.Equal("EA", row.BaseUomId);
        Assert.Equal(2m, row.ConversionFactor);
        Assert.Equal("MDM-UOM", row.ConversionContractId);
        Assert.Equal("v1", row.ConversionContractVersion);
        Assert.Equal(4m, row.BaseQuantity);
        Assert.Contains(row.Issues, x => x.Code == "StockoutEvidenceUnknown" &&
            x.Severity == ImportIssueSeverity.Warning);
        Assert.Equal("FileBatchValidated", Assert.Single(batch.AuditTrail).Action);
        Assert.True(batch.IsEligibleForIndependentReview());
    }

    [Fact]
    public async Task Create_InvalidRows_RetainsEveryRowAndBlocksWholeBatch()
    {
        var store = new MemoryStore();
        var file = Header + "\n" + Row("OK") + "\n" +
                   Row("BAD", "wh-outside", "2026-10-01", "not-a-number");
        var result = await Handler(store).Handle(Command(file), default);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(2, result.Data!.RowCount);
        Assert.Equal(1, result.Data.QuarantineCount);
        Assert.True(result.Data.ApprovalBlocked);
        Assert.Equal(ImportBatchValidationState.Blocked, result.Data.ValidationState);
        var batch = Assert.Single(store.Batches);
        Assert.Equal(2, batch.Rows.Count);
        Assert.Equal("BAD", batch.Rows[1].SourceRecordKey);
        Assert.Contains(batch.Rows[1].Issues, x => x.Code == "WarehouseOutsideScope");
        Assert.Contains(batch.Rows[1].Issues, x => x.Code == "DateOutsideScope");
        Assert.Contains(batch.Rows[1].Issues, x => x.Code == "InvalidQuantity");
        Assert.Equal(batch.Rows[1].RowNumber, Assert.Single(batch.QuarantineCases).RowNumber);
        Assert.False(batch.IsEligibleForIndependentReview());
    }

    [Fact]
    public async Task Create_InvalidHeaderAndBlankRow_AreVisibleAndNeverSilentlyDropped()
    {
        var store = new MemoryStore();
        var result = await Handler(store).Handle(Command("wrong,header\n" + Row() + "\n\n"), default);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(2, result.Data!.RowCount);
        Assert.Equal(2, result.Data.QuarantineCount);
        Assert.Contains(result.Data.FileIssues, x => x.Code == "InvalidHeader");
        var batch = Assert.Single(store.Batches);
        Assert.Contains(batch.Rows[0].Issues, x => x.Code == "InvalidHeader");
        Assert.Contains(batch.Rows[1].Issues, x => x.Code == "BlankRow");
        Assert.True(batch.ApprovalBlocked);
    }

    [Fact]
    public async Task Create_RepeatedSourceKeyWithinFile_QuarantinesBothRows()
    {
        var store = new MemoryStore();
        var result = await Handler(store).Handle(Command(Header + "\n" + Row() + "\n" + Row()), default);
        Assert.Equal(2, result.Data!.QuarantineCount);
        Assert.All(store.Batches.Single().Rows, x =>
            Assert.Contains(x.Issues, issue => issue.Code == "DuplicateKeyInBatch"));
        Assert.True(result.Data.ApprovalBlocked);
    }

    [Theory]
    [InlineData(HistoryReferenceState.Invalid, "InvalidReference")]
    [InlineData(HistoryReferenceState.Unavailable, "ReferenceOrConversionUnverified")]
    public async Task Create_UnverifiedSkuWarehouseOrConversion_QuarantinesRow(
        HistoryReferenceState state, string code)
    {
        var store = new MemoryStore();
        var result = await Handler(store, references: new FakeReferences(state))
            .Handle(Command(Header + "\n" + Row()), default);
        Assert.Equal(201, result.StatusCode);
        Assert.True(result.Data!.ApprovalBlocked);
        Assert.Equal(1, result.Data.QuarantineCount);
        Assert.Contains(store.Batches.Single().Rows.Single().Issues, x => x.Code == code);
        Assert.Null(store.Batches.Single().Rows.Single().BaseQuantity);
    }

    [Fact]
    public async Task Create_VerifiedReferenceWithoutDatedConversion_QuarantinesRow()
    {
        var store = new MemoryStore();
        var missing = new HistoryReferenceResult(HistoryReferenceState.Verified,
            "EA", null, "MDM-UOM", "v1");
        var result = await Handler(store, references: new FakeReferences(
            HistoryReferenceState.Verified, missing))
            .Handle(Command(Header + "\n" + Row()), default);
        Assert.Equal(201, result.StatusCode);
        Assert.True(result.Data!.ApprovalBlocked);
        Assert.Contains(store.Batches.Single().Rows.Single().Issues,
            x => x.Code == "ReferenceOrConversionUnverified");
    }

    [Fact]
    public async Task Create_TenantAndLegalEntityScopeArePassedToAuthority()
    {
        var store = new MemoryStore();
        var authority = new FakeAuthority(true) { ExpectedTenant = Tenant };
        var result = await Handler(store, authority).Handle(Command(Header + "\n" + Row(),
            tenant: OtherTenant), default);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(OtherTenant, authority.LastTenant);
        Assert.Equal(Guid.Empty, authority.LastLegalEntity);
        Assert.Empty(store.Batches);
    }
    [Fact]
    public async Task Create_AdjustmentWithoutLink_RemainsQuarantinedForLaterReview()
    {
        var store = new MemoryStore();
        var result = await Handler(store).Handle(Command(Header + "\n" +
            Row(kind: "Cancellation")), default);
        Assert.True(result.Data!.ApprovalBlocked);
        var issues = Assert.Single(store.Batches.Single().Rows).Issues;
        Assert.Contains(issues, x => x.Code == "MissingRelatedRecord");
        Assert.Contains(issues, x => x.Code == "AdjustmentPendingReview");
    }

    [Fact]
    public async Task Create_UnauthorizedOrUnavailableScope_FailsClosedWithoutPersistence()
    {
        var store = new MemoryStore();
        var file = Header + "\n" + Row();
        var denied = await Handler(store, new FakeAuthority(false)).Handle(Command(file), default);
        var unavailable = await Handler(store, new FakeAuthority(null)).Handle(Command(file), default);
        Assert.Equal(404, denied.StatusCode);
        Assert.Equal(503, unavailable.StatusCode);
        Assert.Empty(store.Batches);
    }

    [Fact]
    public async Task Create_UsesIndependentAssignmentForTwoLegalEntitiesAndRejectsUnassignedSelection()
    {
        var store = new MemoryStore();
        var authority = new FakeAuthority(true);
        authority.AssignedLegalEntities.Add(OtherLegalEntity);
        var handler = Handler(store, authority);
        var file = Header + "\n" + Row();
        var first = await handler.Handle(Command(file, key: "first"), default);
        var second = await handler.Handle(Command(file, key: "second",
            legalEntityHint: OtherLegalEntity), default);
        authority.AssignedLegalEntities.Remove(OtherLegalEntity);
        var revoked = await handler.Handle(Command(file, key: "third",
            legalEntityHint: OtherLegalEntity), default);
        var missing = await handler.Handle(Command(file) with
            { SelectedLegalEntityHint = Guid.Empty }, default);
        Assert.Equal(201, first.StatusCode);
        Assert.Equal(201, second.StatusCode);
        Assert.Equal(LegalEntity, first.Data!.LegalEntityId);
        Assert.Equal(OtherLegalEntity, second.Data!.LegalEntityId);
        Assert.Equal(404, revoked.StatusCode);
        Assert.Equal(400, missing.StatusCode);
        Assert.Equal(2, store.Batches.Count);
    }

    [Fact]
    public async Task DifferentResolvedLegalEntityCannotCreateImportOrReadOrReviewAnotherEntityBatch()
    {
        var store = new MemoryStore();
        var authority = new FakeAuthority(true);
        authority.AssignedLegalEntities.Add(OtherLegalEntity);
        var file = Header + "\n" + Row();
        var batchId = (await Handler(store, authority).Handle(Command(file,
            legalEntityHint: OtherLegalEntity), default)).Data!.BatchId;
        var normalOwn = await new GetOwnDemandHistoryImportBatchHandler(store, authority)
            .Handle(new(Tenant, Actor, batchId, OtherLegalEntity), default);
        var normalReviewView = await new GetReviewHistoryImportBatchHandler(store,
            new MemoryReviewAudit(), authority)
            .Handle(new(Tenant, OtherActor, batchId, OtherLegalEntity, true), default);
        authority.ResolvedOverride = OtherLegalEntity;

        var create = await Handler(store, authority).Handle(Command(file,
            key: "wrong-company", legalEntityHint: LegalEntity), default);
        var own = await new GetOwnDemandHistoryImportBatchHandler(store, authority)
            .Handle(new(Tenant, Actor, batchId, LegalEntity), default);
        var audit = new MemoryReviewAudit();
        var reviewView = await new GetReviewHistoryImportBatchHandler(store, audit, authority)
            .Handle(new(Tenant, OtherActor, batchId, LegalEntity, true), default);
        var decision = await new ReviewHistoryImportBatchHandler(store, audit, authority)
            .Handle(Review(batchId, legalEntityHint: LegalEntity), default);

        Assert.Equal(200, normalOwn.StatusCode);
        Assert.Equal(200, normalReviewView.StatusCode);
        Assert.Equal(503, create.StatusCode);
        Assert.Equal(503, own.StatusCode);
        Assert.Equal(503, reviewView.StatusCode);
        Assert.Equal(503, decision.StatusCode);
        Assert.Single(store.Batches);
        Assert.Equal(ImportBatchReviewState.Pending, store.Batches.Single().ReviewState);
        Assert.Equal("AssignmentMismatch", Assert.Single(audit.Failures).Outcome);
    }

    [Fact]
    public async Task Create_AssignmentSourceOutageFailsClosedBeforeAnyBatchWrite()
    {
        var store = new MemoryStore();
        var authority = new FakeAuthority(true) { AssignmentUnavailable = true };
        var result = await Handler(store, authority)
            .Handle(Command(Header + "\n" + Row()), default);
        Assert.Equal(503, result.StatusCode);
        Assert.Empty(store.Batches);
    }

    [Fact]
    public async Task UnconfiguredProductionAssignmentAdapterNeverCreatesImportBatch()
    {
        var store = new MemoryStore();
        var result = await Handler(store, new UnconfiguredHistoryImportReferences())
            .Handle(Command(Header + "\n" + Row()), default);
        Assert.Equal(503, result.StatusCode);
        Assert.Empty(store.Batches);
    }

    [Fact]
    public async Task Create_SameKeySameFileIsIdempotent_ChangedFileConflicts()
    {
        var store = new MemoryStore();
        var handler = Handler(store);
        var original = await handler.Handle(Command(Header + "\n" + Row()), default);
        var retry = await handler.Handle(Command(Header + "\n" + Row()), default);
        var changed = await handler.Handle(Command(Header + "\n" + Row(quantity: "3")), default);
        Assert.Equal(201, original.StatusCode);
        Assert.Equal(200, retry.StatusCode);
        Assert.Equal(original.Data!.BatchId, retry.Data!.BatchId);
        Assert.Equal(409, changed.StatusCode);
        Assert.Single(store.Batches);
    }

    [Fact]
    public async Task GetOwn_DeniesOtherTenantActorAndRevokedLegalEntityScope()
    {
        var store = new MemoryStore();
        var authority = new FakeAuthority(true);
        var created = await Handler(store, authority).Handle(Command(Header + "\n" + Row()), default);
        var query = new GetOwnDemandHistoryImportBatchHandler(store, authority);
        var id = created.Data!.BatchId;
        Assert.Equal(200, (await query.Handle(new(Tenant, Actor, id, LegalEntity), default)).StatusCode);
        Assert.Equal(404, (await query.Handle(new(OtherTenant, Actor, id, LegalEntity), default)).StatusCode);
        Assert.Equal(404, (await query.Handle(new(Tenant, OtherActor, id, LegalEntity), default)).StatusCode);
        authority.Decision = false;
        Assert.Equal(404, (await query.Handle(new(Tenant, Actor, id, LegalEntity), default)).StatusCode);
        authority.Decision = null;
        Assert.Equal(503, (await query.Handle(new(Tenant, Actor, id, LegalEntity), default)).StatusCode);
    }

    [Fact]
    public async Task ReadAndReviewCannotCrossSelectedLegalEntityEvenWhenBothAreAssigned()
    {
        var store = new MemoryStore();
        var authority = new FakeAuthority(true);
        authority.AssignedLegalEntities.Add(OtherLegalEntity);
        var id = (await Handler(store, authority)
            .Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        var own = await new GetOwnDemandHistoryImportBatchHandler(store, authority)
            .Handle(new(Tenant, Actor, id, OtherLegalEntity), default);
        var reviewView = await new GetReviewHistoryImportBatchHandler(store,
            new MemoryReviewAudit(), authority)
            .Handle(new(Tenant, OtherActor, id, OtherLegalEntity, true), default);
        var audit = new MemoryReviewAudit();
        var decision = await new ReviewHistoryImportBatchHandler(store, audit, authority)
            .Handle(Review(id, legalEntityHint: OtherLegalEntity), default);
        Assert.Equal(404, own.StatusCode);
        Assert.Equal(404, reviewView.StatusCode);
        Assert.Equal(404, decision.StatusCode);
        Assert.Equal(ImportBatchReviewState.Pending, store.Batches.Single().ReviewState);
        Assert.Equal("ScopeDenied", Assert.Single(audit.Failures).Outcome);
    }

    [Fact]
    public async Task Create_TooManyRows_ExplicitlyRejectsRatherThanTruncates()
    {
        var store = new MemoryStore();
        var file = Header + "\n" + string.Join("\n", Enumerable.Range(0, 2001).Select(i => Row($"R-{i}")));
        var result = await Handler(store).Handle(Command(file), default);
        Assert.Equal(413, result.StatusCode);
        Assert.Empty(store.Batches);
    }

    [Fact]
    public async Task Create_InvalidUtf8_PersistsBlockingFileIssue()
    {
        var store = new MemoryStore();
        var command = Command(Header + "\n" + Row()) with { FileBytes = [0xFF, 0xFE] };
        var result = await Handler(store).Handle(command, default);
        Assert.Equal(201, result.StatusCode);
        Assert.True(result.Data!.ApprovalBlocked);
        Assert.Contains(result.Data.FileIssues, x => x.Code == "InvalidUtf8");
        Assert.Empty(store.Batches.Single().Rows);
    }

    [Fact]
    public async Task MongoBsonRoundTrip_PreservesRawRowAndQuarantineEvidence()
    {
        var store = new MemoryStore();
        await Handler(store).Handle(Command(Header + "\n" + Row(quantity: "bad")), default);
        var document = store.Batches.Single().ToBsonDocument();
        var restored = BsonSerializer.Deserialize<DemandHistoryImportBatch>(document);
        Assert.True(restored.ApprovalBlocked);
        Assert.Single(restored.Rows);
        Assert.Equal("bad", restored.Rows[0].RawFields[5]);
        Assert.Equal(restored.Rows[0].RowNumber, Assert.Single(restored.QuarantineCases).RowNumber);
    }

    [Fact]
    public void ApiRoute_DraftV2PathAndImportPermissionAreExplicit()
    {
        var route = Assert.Single(typeof(DemandHistoryImportBatchesController)
            .GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>());
        Assert.Equal("api/v2/demand/history-import-batches", route.Template);
        var create = typeof(DemandHistoryImportBatchesController).GetMethod("Create")!;
        var permission = Assert.Single(create
            .GetCustomAttributes(typeof(HasPermissionAttribute), false).Cast<HasPermissionAttribute>());
        Assert.Equal("demand.history-imports.import", permission.Policy);
        Assert.Null(typeof(CreateHistoryImportForm).GetProperty("LegalEntityId"));
    }

    [Fact]
    public async Task ImportApiRejectsLegalEntityInMultipartPayloadBeforeSendingCommand()
    {
        var http = new DefaultHttpContext();
        http.Items["DemandPlanning.TenantId"] = Tenant;
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Actor.ToString("D"))], "test"));
        http.Request.Form = new FormCollection(new Dictionary<string,
            Microsoft.Extensions.Primitives.StringValues>
        {
            ["LegalEntityId"] = OtherLegalEntity.ToString("D")
        });
        var controller = new DemandHistoryImportBatchesController(
            DispatchProxy.Create<ISender, RejectAllSenderProxy>())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        var response = await controller.Create(new CreateHistoryImportForm(), LegalEntity,
            "request-1", default);
        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Fact]
    public void ReviewApiRejectsClientSuppliedLegalEntityInJsonPayload()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReviewHistoryImportForm>(
            """{"decision":1,"reason":"Checked","legalEntityId":"88888888-8888-8888-8888-888888888888"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public async Task Review_WarningOnlyBatch_DifferentActorApprovesWithDurableDecisionEvidence()
    {
        var store = new MemoryStore();
        var audit = new MemoryReviewAudit();
        var created = await Handler(store).Handle(Command(Header + "\n" + Row()), default);
        var reviewer = new ReviewHistoryImportBatchHandler(store, audit, new FakeAuthority(true));
        var result = await reviewer.Handle(Review(created.Data!.BatchId), default);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(ImportBatchReviewState.Approved, result.Data!.ReviewState);
        Assert.Equal(OtherActor, result.Data.ReviewedByActorId);
        Assert.NotNull(result.Data.ReviewedAt);
        Assert.Equal("Checked source evidence", result.Data.ReviewReason);
        Assert.Equal("StockoutEvidenceUnknown", Assert.Single(result.Data.Rows).Issues.Single().Code);
        var saved = Assert.Single(store.Batches);
        Assert.Equal(1, saved.Version);
        var decision = Assert.Single(saved.AuditTrail, x => x.Action == "BatchApproved");
        Assert.Equal(OtherActor, decision.ActorId);
        Assert.Equal("review-1", decision.RequestKey);
        Assert.Equal("Checked source evidence", decision.Reason);
        Assert.Empty(audit.Failures);
        Assert.Single(saved.Rows); // Still only unaccepted import rows.
    }

    [Fact]
    public async Task Review_UploaderCannotApproveOwnBatch_FailureIsAudited()
    {
        var store = new MemoryStore();
        var audit = new MemoryReviewAudit();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        var reviewer = new ReviewHistoryImportBatchHandler(store, audit, new FakeAuthority(true));
        var result = await reviewer.Handle(Review(id, actor: Actor), default);
        var retry = await reviewer.Handle(Review(id, actor: Actor), default);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(403, retry.StatusCode);
        Assert.Equal(ImportBatchReviewState.Pending, store.Batches.Single().ReviewState);
        Assert.Equal("SelfReviewDenied", Assert.Single(audit.Failures).Outcome);
    }

    [Fact]
    public async Task Review_BlockingIssueOrOpenQuarantine_PreventsWholeBatchApproval()
    {
        var store = new MemoryStore();
        var audit = new MemoryReviewAudit();
        var id = (await Handler(store).Handle(Command(Header + "\n" +
            Row("OK") + "\n" + Row("BAD", quantity: "bad")), default)).Data!.BatchId;
        var reviewer = new ReviewHistoryImportBatchHandler(store, audit, new FakeAuthority(true));
        Assert.Equal(409, (await reviewer.Handle(Review(id), default)).StatusCode);
        Assert.Equal("BlockingValidation", Assert.Single(audit.Failures).Outcome);
        Assert.Equal(ImportBatchReviewState.Pending, store.Batches.Single().ReviewState);
        var rejected = await reviewer.Handle(Review(id, decision: ImportBatchReviewState.Rejected,
            reason: "Invalid quantity", key: "reject-1"), default);
        Assert.Equal(200, rejected.StatusCode);
        Assert.Equal(ImportBatchReviewState.Rejected, rejected.Data!.ReviewState);
    }

    [Fact]
    public async Task Review_OpenQuarantineBlocksEvenIfValidationFlagIsStale()
    {
        var store = new MemoryStore();
        var audit = new MemoryReviewAudit();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        store.Batches.Single().QuarantineCases.Add(new DemandHistoryQuarantineCase
        { RowNumber = 2, State = "Open", ReasonCodes = ["Unresolved"] });
        store.Batches.Single().ApprovalBlocked = false;
        var result = await new ReviewHistoryImportBatchHandler(store, audit, new FakeAuthority(true))
            .Handle(Review(id), default);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ImportBatchReviewState.Pending, store.Batches.Single().ReviewState);
    }

    [Fact]
    public async Task Review_BlockingFileIssueCannotBeHiddenByStaleDerivedFlags()
    {
        var store = new MemoryStore();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        var batch = Assert.Single(store.Batches);
        batch.ApprovalBlocked = false;
        batch.ValidationState = ImportBatchValidationState.ValidatedWithWarnings;
        batch.FileIssues.Add(new ImportValidationIssue
        { Code = "UnverifiedFile", Severity = ImportIssueSeverity.Blocking });
        Assert.False(batch.IsEligibleForIndependentReview());
        var result = await new ReviewHistoryImportBatchHandler(store,
            new MemoryReviewAudit(), new FakeAuthority(true)).Handle(Review(id), default);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ImportBatchReviewState.Pending, batch.ReviewState);
    }

    [Fact]
    public async Task Review_BlockingRowIssueCannotBeHiddenByStaleDerivedFlags()
    {
        var store = new MemoryStore();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        var batch = Assert.Single(store.Batches);
        batch.ApprovalBlocked = false;
        batch.ValidationState = ImportBatchValidationState.ValidatedWithWarnings;
        batch.Rows.Single().Issues.Add(new ImportValidationIssue
        { Code = "UnverifiedRow", Severity = ImportIssueSeverity.Blocking });
        Assert.False(batch.IsEligibleForIndependentReview());
        var result = await new ReviewHistoryImportBatchHandler(store,
            new MemoryReviewAudit(), new FakeAuthority(true)).Handle(Review(id), default);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ImportBatchReviewState.Pending, batch.ReviewState);
    }

    [Fact]
    public void MongoApprovalFilter_RechecksBlockingFileAndNestedRowIssues()
    {
        var filter = DemandHistoryImportMongoStore.BuildApprovalEligibilityFilter();
        var rendered = filter.Render(BsonSerializer.LookupSerializer<DemandHistoryImportBatch>(),
            BsonSerializer.SerializerRegistry).ToJson();
        Assert.Matches("\\\"FileIssues\\\"\\s*:\\s*\\{\\s*\\\"\\$not\\\"", rendered);
        Assert.Matches("\\\"Rows\\\"\\s*:\\s*\\{\\s*\\\"\\$not\\\"", rendered);
        Assert.Matches("\\\"Issues\\\"\\s*:\\s*\\{\\s*\\\"\\$elemMatch\\\"", rendered);
        Assert.Matches("\\\"QuarantineCases\\\"\\s*:\\s*\\{\\s*\\\"\\$not\\\"", rendered);
        Assert.Equal(2, Regex.Matches(rendered, @"""Severity""\s*:\s*1").Count);
    }

    [Fact]
    public async Task Review_PermissionAndScopeFailClosed_AndReviewerHasSeparateReadView()
    {
        var store = new MemoryStore();
        var audit = new MemoryReviewAudit();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        var authority = new FakeAuthority(true);
        var reviewer = new ReviewHistoryImportBatchHandler(store, audit, authority);
        Assert.Equal(403, (await reviewer.Handle(Review(id, permission: false), default)).StatusCode);
        Assert.Equal(404, (await reviewer.Handle(Review(id, tenant: OtherTenant,
            key: "other-tenant"), default)).StatusCode);
        authority.Decision = false;
        Assert.Equal(404, (await reviewer.Handle(Review(id, key: "scope-denied"), default)).StatusCode);
        authority.Decision = null;
        Assert.Equal(503, (await reviewer.Handle(Review(id, key: "scope-unavailable"), default)).StatusCode);
        Assert.Equal(ImportBatchReviewState.Pending, store.Batches.Single().ReviewState);
        Assert.Contains(audit.Failures, x => x.Outcome == "PermissionDenied");
        Assert.Contains(audit.Failures, x => x.Outcome == "ScopeDenied");
        authority.Decision = true;
        var get = new GetReviewHistoryImportBatchHandler(store, audit, authority);
        var view = await get.Handle(new(Tenant, OtherActor, id, LegalEntity, true), default);
        Assert.Equal(200, view.StatusCode);
        Assert.Equal(Actor, view.Data!.Batch.UploadedByActorId);
        Assert.Equal(3, view.Data.FailedAttempts.Count);
        Assert.Equal(403, (await get.Handle(new(Tenant, OtherActor, id, LegalEntity, false), default)).StatusCode);
        Assert.Equal(404, (await get.Handle(new(OtherTenant, OtherActor, id, LegalEntity, true), default)).StatusCode);
        Assert.Equal(404, (await new GetOwnDemandHistoryImportBatchHandler(store, authority)
            .Handle(new(Tenant, OtherActor, id, LegalEntity), default)).StatusCode);
    }

    [Fact]
    public async Task Review_SameRequestIsIdempotent_DifferentRequestConflicts()
    {
        var store = new MemoryStore();
        var audit = new MemoryReviewAudit();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        var reviewer = new ReviewHistoryImportBatchHandler(store, audit, new FakeAuthority(true));
        var first = await reviewer.Handle(Review(id), default);
        var retry = await reviewer.Handle(Review(id), default);
        var changed = await reviewer.Handle(Review(id, reason: "Different rationale"), default);
        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, retry.StatusCode);
        Assert.Equal(first.Data!.ReviewedAt, retry.Data!.ReviewedAt);
        Assert.Equal(409, changed.StatusCode);
        Assert.Equal(1, store.Batches.Single().Version);
        Assert.Single(store.Batches.Single().AuditTrail, x => x.Action == "BatchApproved");
        Assert.Equal("DecisionConflict", Assert.Single(audit.Failures).Outcome);
    }

    [Fact]
    public async Task Review_MissingReasonCannotCreateDecision_AndIsAudited()
    {
        var store = new MemoryStore();
        var audit = new MemoryReviewAudit();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        var result = await new ReviewHistoryImportBatchHandler(store, audit, new FakeAuthority(true))
            .Handle(Review(id, reason: "  "), default);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ImportBatchReviewState.Pending, store.Batches.Single().ReviewState);
        Assert.Equal("InvalidReviewRequest", Assert.Single(audit.Failures).Outcome);
    }

    [Fact]
    public async Task Review_FailedAttemptAuditOutage_Returns503WithoutDecision()
    {
        foreach (var exception in new Exception[]
        {
            new IOException("audit disk unavailable"),
            new MongoException("audit Mongo unavailable"),
            new OperationCanceledException("internal audit timeout")
        })
        {
            var store = new MemoryStore();
            var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
            var audit = new ThrowingReviewAudit(exception);
            var result = await new ReviewHistoryImportBatchHandler(store, audit,
                new FakeAuthority(true)).Handle(Review(id, actor: Actor), CancellationToken.None);
            Assert.Equal(503, result.StatusCode);
            Assert.Equal("Review audit is unavailable.", Assert.Single(result.Errors));
            Assert.Equal(1, audit.AppendCalls);
            var batch = Assert.Single(store.Batches);
            Assert.Equal(ImportBatchReviewState.Pending, batch.ReviewState);
            Assert.DoesNotContain(batch.AuditTrail,
                x => x.Action is "BatchApproved" or "BatchRejected");
        }
    }

    [Fact]
    public async Task ReviewAuthorizationFailure_AuditOutageReturns503Before403()
    {
        var audit = new ThrowingReviewAudit(new MongoException("audit Mongo unavailable"));
        var handler = new DemandReviewAuthorizationResultHandler(audit);
        var context = new DefaultHttpContext();
        var batchId = Guid.NewGuid();
        context.Request.Method = "POST";
        context.Request.Path = $"/api/v2/demand/history-import-batches/{batchId:D}/review";
        context.Request.RouteValues["batchId"] = batchId.ToString("D");
        context.Items["DemandPlanning.TenantId"] = Tenant;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, OtherActor.ToString("D"))], "test"));
        await handler.HandleAsync(_ => Task.CompletedTask, context,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Forbid());
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal(1, audit.AppendCalls);
    }

    [Fact]
    public async Task ReviewerRead_AuditOutageReturnsExplicit503()
    {
        var store = new MemoryStore();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        var result = await new GetReviewHistoryImportBatchHandler(store,
            new ThrowingReviewAudit(new MongoException("audit read unavailable")),
            new FakeAuthority(true)).Handle(new(Tenant, OtherActor, id, LegalEntity, true), default);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("Review audit is unavailable.", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task Review_ConcurrentDifferentDecisions_OnlyOneWins()
    {
        var store = new MemoryStore();
        var audit = new MemoryReviewAudit();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        var reviewer = new ReviewHistoryImportBatchHandler(store, audit, new FakeAuthority(true));
        var outcomes = await Task.WhenAll(
            reviewer.Handle(Review(id, actor: OtherActor, key: "approve"), default),
            reviewer.Handle(Review(id, actor: ThirdActor,
                decision: ImportBatchReviewState.Rejected, key: "reject"), default));
        Assert.Equal(1, outcomes.Count(x => x.StatusCode == 200));
        Assert.Equal(1, outcomes.Count(x => x.StatusCode == 409));
        Assert.Equal(1, store.Batches.Single().Version);
        Assert.Single(store.Batches.Single().AuditTrail,
            x => x.Action is "BatchApproved" or "BatchRejected");
        Assert.Equal("DecisionConflict", Assert.Single(audit.Failures).Outcome);
    }

    [Fact]
    public async Task Csv_QuotedMultilineFieldIsOneRecord_UnclosedQuoteIsQuarantined()
    {
        var store = new MemoryStore();
        var quoted = Header + "\n" + Row(key: "\"SHIP\n1\"");
        var valid = await Handler(store).Handle(Command(quoted), default);
        Assert.Equal(1, valid.Data!.RowCount);
        Assert.Equal(0, valid.Data.QuarantineCount);
        Assert.Equal("SHIP\n1", store.Batches.Single().Rows.Single().SourceRecordKey);
        Assert.Equal(2, store.Batches.Single().Rows.Single().RowNumber);
        var broken = await Handler(store).Handle(Command(Header + "\n\"never closed",
            key: "broken"), default);
        Assert.Equal(1, broken.Data!.RowCount);
        Assert.Equal(1, broken.Data.QuarantineCount);
        Assert.Contains(broken.Data.Rows.Single().Issues, x => x.Code == "MalformedCsv");
    }

    [Fact]
    public void ReviewApi_UsesSeparatePermissionAndDoesNotRelaxOwnBatchRoute()
    {
        var review = typeof(DemandHistoryImportBatchesController).GetMethod("Review")!;
        var read = typeof(DemandHistoryImportBatchesController).GetMethod("GetForReview")!;
        var own = typeof(DemandHistoryImportBatchesController).GetMethod("GetOwn")!;
        Assert.Equal("demand.history-imports.review", Assert.Single(review
            .GetCustomAttributes(typeof(HasPermissionAttribute), false)
            .Cast<HasPermissionAttribute>()).Policy);
        Assert.Equal("demand.history-imports.review", Assert.Single(read
            .GetCustomAttributes(typeof(HasPermissionAttribute), false)
            .Cast<HasPermissionAttribute>()).Policy);
        Assert.Equal("demand.history-imports.import", Assert.Single(own
            .GetCustomAttributes(typeof(HasPermissionAttribute), false)
            .Cast<HasPermissionAttribute>()).Policy);
    }

    [Fact]
    public async Task ReviewAuthorizationFailure_RecordsAttributablePermissionDenial()
    {
        var audit = new MemoryReviewAudit();
        var handler = new DemandReviewAuthorizationResultHandler(audit);
        var context = new DefaultHttpContext();
        var batchId = Guid.NewGuid();
        context.Request.Method = "POST";
        context.Request.Path = $"/api/v2/demand/history-import-batches/{batchId:D}/review";
        context.Request.RouteValues["batchId"] = batchId.ToString("D");
        context.Request.Headers["Idempotency-Key"] = "denied-1";
        context.Items["DemandPlanning.TenantId"] = Tenant;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, OtherActor.ToString("D"))], "test"));
        var auth = new FakeAuthenticationService();
        context.RequestServices = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
        await handler.HandleAsync(_ => Task.CompletedTask, context,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Forbid());
        Assert.Equal(403, context.Response.StatusCode);
        var failure = Assert.Single(audit.Failures);
        Assert.Equal("PermissionDenied", failure.Outcome);
        Assert.Equal(OtherActor, failure.ActorId);
        Assert.Equal(batchId, failure.BatchId);
        Assert.Equal(Tenant, failure.TenantId);
    }

    [Fact]
    public async Task ReviewDecision_BsonRoundTripPreservesActorReasonTimeAndAudit()
    {
        var store = new MemoryStore();
        var id = (await Handler(store).Handle(Command(Header + "\n" + Row()), default)).Data!.BatchId;
        await new ReviewHistoryImportBatchHandler(store, new MemoryReviewAudit(), new FakeAuthority(true))
            .Handle(Review(id), default);
        var restored = BsonSerializer.Deserialize<DemandHistoryImportBatch>(
            store.Batches.Single().ToBsonDocument());
        Assert.Equal(ImportBatchReviewState.Approved, restored.ReviewState);
        Assert.Equal(OtherActor, restored.ReviewedByActorId);
        Assert.NotNull(restored.ReviewedAt);
        Assert.Equal("Checked source evidence", restored.ReviewReason);
        Assert.Single(restored.AuditTrail, x => x.Action == "BatchApproved");
    }

    private sealed class FakeAuthority : IHistoryImportScopeAuthority
    {
        public bool? Decision { get; set; }
        public Guid ExpectedTenant { get; set; } = Tenant;
        public bool AssignmentUnavailable { get; set; }
        public Guid? ResolvedOverride { get; set; }
        public HashSet<Guid> AssignedLegalEntities { get; } = [LegalEntity];
        public HashSet<Guid> AssignedActors { get; } = [Actor, OtherActor, ThirdActor];
        public Guid LastTenant { get; private set; }
        public Guid LastLegalEntity { get; private set; }
        public FakeAuthority(bool? decision) => Decision = decision;
        public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken)
        {
            LastTenant = tenantId;
            if (AssignmentUnavailable)
                throw new InvalidOperationException("assignment source unavailable");
            return Task.FromResult<Guid?>(tenantId == ExpectedTenant &&
                AssignedActors.Contains(actorId) &&
                AssignedLegalEntities.Contains(selectedLegalEntityHint)
                ? ResolvedOverride ?? selectedLegalEntityHint : null);
        }
        public Task<bool?> IsAuthorizedAsync(Guid tenantId, Guid actorId, Guid legalEntityId,
            DateOnly from, DateOnly through, IReadOnlyList<string> warehouses,
            CancellationToken cancellationToken)
        {
            LastTenant = tenantId;
            LastLegalEntity = legalEntityId;
            return Task.FromResult(tenantId != ExpectedTenant ? (bool?)false : Decision);
        }
    }

    public class RejectAllSenderProxy : DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod,
            object?[]? args) => throw new InvalidOperationException("Sender must not be invoked.");
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme,
            AuthenticationProperties? properties)
        { context.Response.StatusCode = 403; return Task.CompletedTask; }
        public Task SignInAsync(HttpContext context, string? scheme,
            ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class FakeReferences : IHistoryRowReferenceChecker
    {
        private readonly HistoryReferenceState _state;
        private readonly HistoryReferenceResult? _override;
        public FakeReferences(HistoryReferenceState state = HistoryReferenceState.Verified,
            HistoryReferenceResult? overrideResult = null)
        { _state = state; _override = overrideResult; }
        public Task<HistoryReferenceResult> CheckAsync(Guid tenantId, Guid legalEntityId,
            Guid skuId, string skuLevel, string warehouseId, DateOnly occurredOn,
            string originalUomId, CancellationToken cancellationToken) =>
            Task.FromResult(_override ?? new HistoryReferenceResult(_state,
                _state == HistoryReferenceState.Verified ? "EA" : null,
                _state == HistoryReferenceState.Verified ? 2m : null,
                _state == HistoryReferenceState.Verified ? "MDM-UOM" : null,
                _state == HistoryReferenceState.Verified ? "v1" : null));
    }

    private sealed class MemoryReviewAudit : IHistoryImportReviewAuditStore
    {
        private readonly object _gate = new();
        public List<HistoryImportReviewFailure> Failures { get; } = [];
        public Task AppendOnceAsync(HistoryImportReviewFailure failure,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (!Failures.Any(x => x.TenantId == failure.TenantId &&
                    x.BatchId == failure.BatchId && x.ActorId == failure.ActorId &&
                    x.RequestKey == failure.RequestKey && x.Fingerprint == failure.Fingerprint &&
                    x.Outcome == failure.Outcome))
                    Failures.Add(failure);
            }
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<HistoryImportReviewFailure>> ListAsync(Guid tenantId,
            Guid batchId, CancellationToken cancellationToken)
        {
            lock (_gate)
                return Task.FromResult<IReadOnlyList<HistoryImportReviewFailure>>(
                    Failures.Where(x => x.TenantId == tenantId && x.BatchId == batchId).ToList());
        }
    }

    private sealed class ThrowingReviewAudit(Exception failure) : IHistoryImportReviewAuditStore
    {
        public int AppendCalls { get; private set; }
        public Task AppendOnceAsync(HistoryImportReviewFailure attempt,
            CancellationToken cancellationToken)
        { AppendCalls++; throw failure; }
        public Task<IReadOnlyList<HistoryImportReviewFailure>> ListAsync(Guid tenantId,
            Guid batchId, CancellationToken cancellationToken) => throw failure;
    }

    private sealed class MemoryStore : IHistoryImportBatchStore
    {
        private readonly object _gate = new();
        public List<DemandHistoryImportBatch> Batches { get; } = [];
        public Task<HistoryImportInsertResult> InsertOrGetAsync(DemandHistoryImportBatch batch,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var existing = Batches.SingleOrDefault(x => x.TenantId == batch.TenantId &&
                    x.LegalEntityId == batch.LegalEntityId && x.UploadedByActorId == batch.UploadedByActorId &&
                    x.IdempotencyKey == batch.IdempotencyKey);
                if (existing is not null)
                    return Task.FromResult(existing.RequestFingerprint == batch.RequestFingerprint
                        ? new HistoryImportInsertResult(HistoryImportInsertOutcome.Existing, existing)
                        : new HistoryImportInsertResult(HistoryImportInsertOutcome.Conflict, null));
                Batches.Add(batch);
                return Task.FromResult(new HistoryImportInsertResult(HistoryImportInsertOutcome.Created, batch));
            }
        }
        public Task<DemandHistoryImportBatch?> GetOwnAsync(Guid tenantId, Guid actorId,
            Guid batchId, CancellationToken cancellationToken) =>
            Task.FromResult(Batches.SingleOrDefault(x => x.TenantId == tenantId &&
                x.UploadedByActorId == actorId && x.Id == batchId));
        public Task<DemandHistoryImportBatch?> GetForReviewAsync(Guid tenantId,
            Guid batchId, CancellationToken cancellationToken) =>
            Task.FromResult(Batches.SingleOrDefault(x => x.TenantId == tenantId && x.Id == batchId));
        public Task<DemandHistoryImportBatch?> TryDecideAsync(DemandHistoryImportBatch expected,
            Guid reviewerId, ImportBatchReviewState decision, string reason,
            string requestKey, string fingerprint, DateTimeOffset decidedAt,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var current = Batches.SingleOrDefault(x => x.TenantId == expected.TenantId &&
                    x.LegalEntityId == expected.LegalEntityId && x.Id == expected.Id &&
                    x.Version == expected.Version && x.ReviewState == ImportBatchReviewState.Pending &&
                    x.UploadedByActorId != reviewerId);
                if (current is null || decision == ImportBatchReviewState.Approved &&
                    !current.IsEligibleForIndependentReview())
                    return Task.FromResult<DemandHistoryImportBatch?>(null);
                current.ReviewState = decision;
                current.ReviewedByActorId = reviewerId;
                current.ReviewedAt = decidedAt;
                current.ReviewReason = reason;
                current.ReviewRequestKey = requestKey;
                current.ReviewRequestFingerprint = fingerprint;
                current.Version++;
                current.AuditTrail.Add(new DemandHistoryImportAuditAction
                {
                    Action = decision == ImportBatchReviewState.Approved ? "BatchApproved" : "BatchRejected",
                    ActorId = reviewerId, OccurredAt = decidedAt,
                    Reason = reason, RequestKey = requestKey
                });
                return Task.FromResult<DemandHistoryImportBatch?>(current);
            }
        }
    }
}



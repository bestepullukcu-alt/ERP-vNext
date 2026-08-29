using Diten.MdmService.Application.Features.Brand.Commands;
using Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.Brand.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.Brand.Queries;
using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Vocabulary;
using Xunit;

namespace Diten.MdmService.Application.Tests.BrandProduct;

// MOD-0290-FU02 — Brand runtime gates (pack §22.1 items 1-9, 23-24, 27).
public sealed class BrandCommandTests
{
    // Gate 1
    [Fact]
    public async Task Create_persists_brand_with_normalized_code()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBrandRepository(tenantId, []);
        var handler = new CreateBrandHandler(repository);

        var response = await handler.Handle(
            new CreateBrandCommand(BrandProductTestData.BrandRequest(code: "br-001"), Actor: "tester"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var created = Assert.Single(repository.Entities);
        Assert.Equal("BR-001", created.BrandCode);
        Assert.Equal("tester", created.CreatedBy);
        Assert.False(created.IsArchived);
    }

    // Gate 2 — TenantId is never accepted from a caller; the repository stamps it from the tenant context.
    [Fact]
    public async Task Create_resolves_tenant_server_side()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBrandRepository(tenantId, []);
        var handler = new CreateBrandHandler(repository);

        await handler.Handle(new CreateBrandCommand(BrandProductTestData.BrandRequest()), CancellationToken.None);

        Assert.Equal(tenantId, Assert.Single(repository.Entities).TenantId);
    }

    // Gate 2 (shape) — the write contract has no TenantId member at all, so it cannot be supplied.
    [Fact]
    public void BrandWriteRequest_has_no_tenant_id_member()
    {
        var members = typeof(Features.Brand.BrandWriteRequest)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("TenantId", members);
    }

    // Gate 3
    [Fact]
    public async Task Create_rejects_duplicate_active_code_with_409()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBrandRepository(tenantId, [BrandProductTestData.Brand(tenantId, code: "BR-001")]);
        var handler = new CreateBrandHandler(repository);

        var response = await handler.Handle(
            new CreateBrandCommand(BrandProductTestData.BrandRequest(code: "BR-001")), CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.BrandCodeDuplicate));
    }

    // Gate 4 — an ARCHIVED code stays permanently reserved; reuse is refused, not silently allowed.
    [Fact]
    public async Task Create_rejects_code_reuse_of_archived_brand()
    {
        var tenantId = Guid.NewGuid();
        var archived = BrandProductTestData.Brand(tenantId, code: "BR-OLD", isArchived: true);
        var repository = new InMemoryBrandRepository(tenantId, [archived]);
        var handler = new CreateBrandHandler(repository);

        var response = await handler.Handle(
            new CreateBrandCommand(BrandProductTestData.BrandRequest(code: "BR-OLD")), CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.BrandCodeDuplicate));
    }

    // Gate 5
    [Fact]
    public async Task Create_rejects_unknown_status_with_400()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBrandRepository(tenantId, []);
        var handler = new CreateBrandHandler(repository);

        var response = await handler.Handle(
            new CreateBrandCommand(BrandProductTestData.BrandRequest(status: "retired")), CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.InvalidBrandStatus));
    }

    // `archived` can only come from the archive endpoint, never from a write payload.
    [Fact]
    public async Task Create_rejects_archived_status_in_payload()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBrandRepository(tenantId, []);
        var handler = new CreateBrandHandler(repository);

        var response = await handler.Handle(
            new CreateBrandCommand(BrandProductTestData.BrandRequest(status: BrandProductVocabulary.StatusArchived)),
            CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.ArchivedStatusNotAssignable));
    }

    // Gate 21
    [Fact]
    public async Task Create_rejects_inverted_effective_window_with_400()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBrandRepository(tenantId, []);
        var handler = new CreateBrandHandler(repository);

        var response = await handler.Handle(
            new CreateBrandCommand(BrandProductTestData.BrandRequest(
                effectiveFrom: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
                effectiveTo: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.InvalidEffectiveWindow));
    }

    // Gate 22 — silent merge is forbidden; a second primary for one source system is a visible conflict.
    [Fact]
    public async Task Create_rejects_second_primary_external_reference_for_same_source()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBrandRepository(tenantId, []);
        var handler = new CreateBrandHandler(repository);

        var references = new List<BrandProductExternalReferenceDto>
        {
            new("LEGACY-CRM", "A-1", null, null, null, true),
            new("LEGACY-CRM", "A-2", null, null, null, true)
        };

        var response = await handler.Handle(
            new CreateBrandCommand(BrandProductTestData.BrandRequest(externalReferences: references)),
            CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.ExternalReferencePrimaryConflict));
    }

    // Gate 6
    [Fact]
    public async Task Archive_is_soft_and_keeps_the_record_readable()
    {
        var tenantId = Guid.NewGuid();
        var brand = BrandProductTestData.Brand(tenantId);
        var repository = new InMemoryBrandRepository(tenantId, [brand]);

        var response = await new ArchiveBrandHandler(repository)
            .Handle(new ArchiveBrandCommand(brand.Id, Actor: "tester"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var stored = Assert.Single(repository.Entities);
        Assert.True(stored.IsArchived);
        Assert.Equal(BrandProductVocabulary.StatusArchived, stored.BrandStatus);
        Assert.NotNull(stored.ArchivedAt);
        Assert.Equal("tester", stored.ArchivedBy);
        Assert.False(stored.IsDeleted); // technical soft-delete is untouched

        var read = await new GetBrandByIdHandler(repository)
            .Handle(new GetBrandByIdQuery(brand.Id), CancellationToken.None);
        Assert.True(read.IsSuccessful);
    }

    [Fact]
    public async Task Archive_is_idempotent()
    {
        var tenantId = Guid.NewGuid();
        var brand = BrandProductTestData.Brand(tenantId, isArchived: true);
        var repository = new InMemoryBrandRepository(tenantId, [brand]);

        var response = await new ArchiveBrandHandler(repository)
            .Handle(new ArchiveBrandCommand(brand.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
    }

    // Gate 7
    [Fact]
    public async Task Update_of_archived_brand_returns_409()
    {
        var tenantId = Guid.NewGuid();
        var brand = BrandProductTestData.Brand(tenantId, code: "BR-A", isArchived: true);
        var repository = new InMemoryBrandRepository(tenantId, [brand]);

        var response = await new UpdateBrandHandler(repository).Handle(
            new UpdateBrandCommand(brand.Id, BrandProductTestData.BrandRequest(code: "BR-A", name: "Renamed")),
            CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.RecordArchived));
    }

    // Codes are stable: a changed code is refused rather than silently ignored.
    [Fact]
    public async Task Update_rejects_code_change_with_409()
    {
        var tenantId = Guid.NewGuid();
        var brand = BrandProductTestData.Brand(tenantId, code: "BR-A");
        var repository = new InMemoryBrandRepository(tenantId, [brand]);

        var response = await new UpdateBrandHandler(repository).Handle(
            new UpdateBrandCommand(brand.Id, BrandProductTestData.BrandRequest(code: "BR-B")),
            CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.CodeImmutable));
    }

    [Fact]
    public async Task Update_applies_editable_fields()
    {
        var tenantId = Guid.NewGuid();
        var brand = BrandProductTestData.Brand(tenantId, code: "BR-A", name: "Old");
        var repository = new InMemoryBrandRepository(tenantId, [brand]);

        var response = await new UpdateBrandHandler(repository).Handle(
            new UpdateBrandCommand(brand.Id, BrandProductTestData.BrandRequest(code: "BR-A", name: "New"), Actor: "editor"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var stored = Assert.Single(repository.Entities);
        Assert.Equal("New", stored.BrandName);
        Assert.Equal("BR-A", stored.BrandCode);
        Assert.Equal("editor", stored.UpdatedBy);
    }

    // Gate 8 — hard delete does not exist. The repository double throws if anything ever reaches for it.
    [Fact]
    public void Brand_feature_exposes_no_delete_command()
    {
        var commandTypes = typeof(CreateBrandCommand).Assembly
            .GetTypes()
            .Where(x => x.Namespace == typeof(CreateBrandCommand).Namespace)
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain(commandTypes, x => x.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    // Gate 9 — a foreign-tenant brand is invisible: not in the list, and 404 (never 200) on direct read.
    [Fact]
    public async Task List_and_read_are_tenant_isolated()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var mine = BrandProductTestData.Brand(tenantId, code: "BR-MINE", name: "Mine");
        var theirs = BrandProductTestData.Brand(otherTenantId, code: "BR-THEIRS", name: "Theirs");
        var repository = new InMemoryBrandRepository(tenantId, [mine, theirs]);

        var list = await new GetBrandListHandler(repository)
            .Handle(new GetBrandListQuery(), CancellationToken.None);

        Assert.True(list.IsSuccessful);
        Assert.Equal("BR-MINE", Assert.Single(list.Data!.Items).BrandCode);

        var read = await new GetBrandByIdHandler(repository)
            .Handle(new GetBrandByIdQuery(theirs.Id), CancellationToken.None);
        Assert.Equal(404, read.StatusCode);
    }

    // Archived rows stay out of the list unless explicitly requested.
    [Fact]
    public async Task List_excludes_archived_unless_requested()
    {
        var tenantId = Guid.NewGuid();
        var active = BrandProductTestData.Brand(tenantId, code: "BR-A", name: "Active");
        var archived = BrandProductTestData.Brand(tenantId, code: "BR-B", name: "Archived", isArchived: true);
        var repository = new InMemoryBrandRepository(tenantId, [active, archived]);
        var handler = new GetBrandListHandler(repository);

        var defaultList = await handler.Handle(new GetBrandListQuery(), CancellationToken.None);
        Assert.Single(defaultList.Data!.Items);

        var withArchived = await handler.Handle(new GetBrandListQuery { IncludeArchived = true }, CancellationToken.None);
        Assert.Equal(2, withArchived.Data!.Items.Count);
    }

    [Fact]
    public async Task List_applies_search_and_status_filters_server_side()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryBrandRepository(tenantId,
        [
            BrandProductTestData.Brand(tenantId, code: "BR-A", name: "Almiba"),
            BrandProductTestData.Brand(tenantId, code: "BR-B", name: "Betamed")
        ]);
        var handler = new GetBrandListHandler(repository);

        var bySearch = await handler.Handle(new GetBrandListQuery { Search = "almi" }, CancellationToken.None);
        Assert.Equal("Almiba", Assert.Single(bySearch.Data!.Items).BrandName);

        var byStatus = await handler.Handle(new GetBrandListQuery { BrandStatus = "draft" }, CancellationToken.None);
        Assert.Empty(byStatus.Data!.Items);
    }
}

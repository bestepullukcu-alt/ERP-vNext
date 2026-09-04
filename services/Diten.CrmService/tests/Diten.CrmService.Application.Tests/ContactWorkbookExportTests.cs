using ClosedXML.Excel;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.ImportExport;
using Diten.CrmService.Application.Features.ImportExport.Handlers;
using Diten.CrmService.Application.Features.ImportExport.Xlsx;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using DomainAccount = Diten.CrmService.Domain.Entities.Account;
using DomainContact = Diten.CrmService.Domain.Entities.Contact;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0150 Contact Import/Export Task 1 — XLSX template + ReferenceData helper + existing-data export.
/// Proves: template/export share one schema (round-trip), reference values come from the MOD-0048 catalog seam only
/// (no hardcoded fallback), historical links and Notes are opt-in, row limits are controlled, audit is PII-safe.
/// Import (upload/parse/dry-run/apply) is deliberately NOT covered — it is Task 2.
/// </summary>
public sealed class ContactWorkbookExportTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AccountA = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    // ---------------- template ----------------

    [Fact]
    public async Task Template_Contains_Instructions_Contacts_AccountLinks_And_ReferenceData_Sheets()
    {
        var handler = TemplateHandler(out _, out _);

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(IncludeAccountsSheet: false), default);

        Assert.True(res.IsSuccessful);
        using var wb = Open(res.Data!);
        Assert.True(wb.Worksheets.Contains(ContactWorkbookSchema.InstructionsSheet));
        Assert.True(wb.Worksheets.Contains(ContactWorkbookSchema.ContactsSheet));
        Assert.True(wb.Worksheets.Contains(ContactWorkbookSchema.AccountLinksSheet));
        Assert.True(wb.Worksheets.Contains(ContactWorkbookSchema.ReferenceDataSheet));
        Assert.False(wb.Worksheets.Contains(ContactWorkbookSchema.AccountsSheet));
    }

    [Fact]
    public async Task Template_Contact_Sheet_Is_Empty_And_Uses_The_Schema_Header()
    {
        var handler = TemplateHandler(out _, out _);

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        using var wb = Open(res.Data!);
        Assert.Equal(ContactWorkbookSchema.ContactColumns, Header(wb, ContactWorkbookSchema.ContactsSheet));
        Assert.Equal(ContactWorkbookSchema.AccountLinkColumns, Header(wb, ContactWorkbookSchema.AccountLinksSheet));
        Assert.Empty(DataRows(wb, ContactWorkbookSchema.ContactsSheet));
    }

    [Fact]
    public async Task Template_Instructions_Explain_Operations_Pii_And_Historical_Lifecycle()
    {
        var handler = TemplateHandler(out _, out _);

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        var text = SheetText(Open(res.Data!), ContactWorkbookSchema.InstructionsSheet);
        Assert.Contains("upload/apply import will be delivered in the next task", text);
        Assert.Contains("Operation", text);
        Assert.Contains("end", text);
        Assert.Contains("personal data", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never deletes it", text);
        Assert.Contains("patient", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Template_Accounts_Sheet_Is_Written_Only_When_Requested()
    {
        var handler = TemplateHandler(out _, out var accounts);
        accounts.Items.Add(new DomainAccount { Id = AccountA, TenantId = TenantA, AccountCode = "ACC-1", AccountName = "City Hospital", AccountType = "hospital" });

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(IncludeAccountsSheet: true), default);

        using var wb = Open(res.Data!);
        Assert.True(wb.Worksheets.Contains(ContactWorkbookSchema.AccountsSheet));
        Assert.Equal(ContactWorkbookSchema.AccountColumns, Header(wb, ContactWorkbookSchema.AccountsSheet));
        Assert.Contains("ACC-1", SheetText(wb, ContactWorkbookSchema.AccountsSheet));
    }

    // ---------------- ReferenceData helper ----------------

    [Fact]
    public async Task ReferenceData_Sheet_Lists_Published_Values_From_The_Mod0048_Seam()
    {
        var handler = TemplateHandler(out var catalog, out _);
        catalog.Publish("contact-type", ("doctor", "Doctor"), ("pharmacist", "Pharmacist"));

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        var rows = DataRows(Open(res.Data!), ContactWorkbookSchema.ReferenceDataSheet)
            .Where(r => r[0] == "contact-type").ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r[1] == "doctor" && r[2] == "Doctor");
        Assert.All(rows, r => Assert.Equal("TRUE", r[4]));
    }

    [Fact]
    public async Task ReferenceData_Sheet_Marks_An_Unpublished_Set_As_Not_Published_And_Adds_No_Fallback_Value()
    {
        var handler = TemplateHandler(out var catalog, out _);
        catalog.Publish("contact-type", ("doctor", "Doctor"));
        // gender intentionally left unpublished

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        var genderRows = DataRows(Open(res.Data!), ContactWorkbookSchema.ReferenceDataSheet)
            .Where(r => r[0] == "gender").ToList();
        var row = Assert.Single(genderRows);
        Assert.Equal(ContactWorkbookSchema.NotPublishedMarker, row[1]);
    }

    [Fact]
    public async Task ReferenceData_Sheet_Contains_Only_Values_The_Catalog_Returned()
    {
        var handler = TemplateHandler(out var catalog, out _);
        catalog.Publish("contact-type", ("doctor", "Doctor"));

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        // Every value code in the sheet is either the one published value or the NOT_PUBLISHED marker — no CRM-local
        // seed list can leak in through the writer.
        var codes = DataRows(Open(res.Data!), ContactWorkbookSchema.ReferenceDataSheet).Select(r => r[1]).Distinct().ToList();
        Assert.Equal(new[] { "doctor", ContactWorkbookSchema.NotPublishedMarker }.OrderBy(x => x), codes.OrderBy(x => x));
    }

    [Fact]
    public async Task ReferenceData_Sheet_Flags_Deprecated_Values_As_Not_Active()
    {
        var handler = TemplateHandler(out var catalog, out _);
        catalog.PublishSnapshot("contact-status", new ReferenceValueSnapshot("active", "Active", null, true, false, null),
            new ReferenceValueSnapshot("legacy", "Legacy", null, false, true, null));

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        var rows = DataRows(Open(res.Data!), ContactWorkbookSchema.ReferenceDataSheet).Where(r => r[0] == "contact-status").ToList();
        Assert.Contains(rows, r => r[1] == "legacy" && r[5] == "TRUE");
        Assert.Contains(rows, r => r[1] == "active" && r[5] == "FALSE");
    }

    [Fact]
    public async Task Template_Binds_A_Dropdown_To_The_Published_Values_Of_A_Set()
    {
        var handler = TemplateHandler(out var catalog, out _);
        catalog.Publish("contact-type", ("doctor", "Doctor"), ("pharmacist", "Pharmacist"));

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        using var wb = Open(res.Data!);
        var validation = DataValidationFor(wb, ContactWorkbookSchema.ContactsSheet, ContactWorkbookSchema.ContactColumns, "ContactType");
        Assert.NotNull(validation);
        // The list source points at the ReferenceData sheet, not at an inline literal list.
        Assert.Contains(ContactWorkbookSchema.ReferenceDataSheet, validation!);
    }

    [Fact]
    public async Task Template_Binds_No_Dropdown_When_The_Set_Is_Not_Published()
    {
        var handler = TemplateHandler(out var catalog, out _);
        catalog.Publish("contact-type", ("doctor", "Doctor"));   // gender left unpublished

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        using var wb = Open(res.Data!);
        Assert.Null(DataValidationFor(wb, ContactWorkbookSchema.ContactsSheet, ContactWorkbookSchema.ContactColumns, "Gender"));
    }

    [Fact]
    public async Task Template_Offers_The_Operation_Keywords_As_A_Fixed_List()
    {
        var handler = TemplateHandler(out _, out _);

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        using var wb = Open(res.Data!);
        var validation = DataValidationFor(wb, ContactWorkbookSchema.ContactsSheet, ContactWorkbookSchema.ContactColumns, ContactWorkbookSchema.OperationColumn);
        // Excel only renders an inline list when formula1 is a QUOTED literal — an unquoted list is parsed as a
        // reference and the dropdown disappears, so assert the exact quoted form, not just "contains the words".
        Assert.Equal("\"add,update,end,skip\"", validation);
        Assert.All(ContactWorkbookSchema.OperationValues, v => Assert.Contains(v, validation!));
    }

    [Fact]
    public async Task AccountLinks_Sheet_Also_Offers_The_Operation_Dropdown()
    {
        var handler = TemplateHandler(out _, out _);

        var res = await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        using var wb = Open(res.Data!);
        var validation = DataValidationFor(wb, ContactWorkbookSchema.AccountLinksSheet, ContactWorkbookSchema.AccountLinkColumns, ContactWorkbookSchema.OperationColumn);
        Assert.Equal("\"add,update,end,skip\"", validation);
    }

    // ---------------- export ----------------

    [Fact]
    public async Task Export_Contacts_Only_Writes_ContactId_Location_And_Gender_For_Round_Trip()
    {
        var handler = ExportHandler(out var contacts, out _, out _, out _, out _);
        var contact = SeedContact(contacts, "Ada", "Lovelace");
        contact.CountryRef = "tr";
        contact.CityRef = "istanbul";
        contact.Gender = "female";

        var res = await handler.Handle(new ExportContactsWorkbookQuery(new ContactWorkbookOptions()), default);

        using var wb = Open(res.Data!);
        Assert.Equal(ContactWorkbookSchema.ContactColumns, Header(wb, ContactWorkbookSchema.ContactsSheet));
        var row = Assert.Single(DataRows(wb, ContactWorkbookSchema.ContactsSheet));
        Assert.Equal(string.Empty, Cell(row, ContactWorkbookSchema.ContactColumns, "Operation"));
        Assert.Equal(contact.Id.ToString(), Cell(row, ContactWorkbookSchema.ContactColumns, "ContactId"));
        Assert.Equal("tr", Cell(row, ContactWorkbookSchema.ContactColumns, "CountryCode"));
        Assert.Equal("female", Cell(row, ContactWorkbookSchema.ContactColumns, "Gender"));
    }

    [Fact]
    public async Task Export_Writes_External_Reference_So_The_Row_Can_Be_Matched_Later()
    {
        var handler = ExportHandler(out var contacts, out var refs, out _, out _, out _);
        var contact = SeedContact(contacts, "Ada", "Lovelace");
        refs.Items.Add(new ContactExternalReference { TenantId = TenantA, ContactId = contact.Id, SourceSystem = "legacy-crm", ExternalId = "EXT-9" });

        var res = await handler.Handle(new ExportContactsWorkbookQuery(new ContactWorkbookOptions()), default);

        var row = Assert.Single(DataRows(Open(res.Data!), ContactWorkbookSchema.ContactsSheet));
        Assert.Equal("legacy-crm", Cell(row, ContactWorkbookSchema.ContactColumns, "ExternalSystem"));
        Assert.Equal("EXT-9", Cell(row, ContactWorkbookSchema.ContactColumns, "ExternalId"));
    }

    [Fact]
    public async Task Export_Without_IncludeLinks_Writes_No_AccountLinks_Sheet()
    {
        var handler = ExportHandler(out var contacts, out _, out var links, out _, out _);
        var contact = SeedContact(contacts, "Ada", "Lovelace");
        links.Items.Add(Link(contact.Id, "active"));

        var res = await handler.Handle(new ExportContactsWorkbookQuery(new ContactWorkbookOptions()), default);

        Assert.False(Open(res.Data!).Worksheets.Contains(ContactWorkbookSchema.AccountLinksSheet));
    }

    [Fact]
    public async Task Export_With_IncludeLinks_Writes_Link_Rows_With_AccountCode_And_Name()
    {
        var handler = ExportHandler(out var contacts, out _, out var links, out var accounts, out _);
        var contact = SeedContact(contacts, "Ada", "Lovelace");
        links.Items.Add(Link(contact.Id, "active"));
        accounts.Items.Add(new DomainAccount { Id = AccountA, TenantId = TenantA, AccountCode = "ACC-1", AccountName = "City Hospital" });

        var res = await handler.Handle(new ExportContactsWorkbookQuery(new ContactWorkbookOptions(IncludeLinks: true)), default);

        using var wb = Open(res.Data!);
        Assert.Equal(ContactWorkbookSchema.AccountLinkColumns, Header(wb, ContactWorkbookSchema.AccountLinksSheet));
        var row = Assert.Single(DataRows(wb, ContactWorkbookSchema.AccountLinksSheet));
        Assert.Equal("ACC-1", Cell(row, ContactWorkbookSchema.AccountLinkColumns, "AccountCode"));
        Assert.Equal("City Hospital", Cell(row, ContactWorkbookSchema.AccountLinkColumns, "AccountName"));
        Assert.Equal(contact.Id.ToString(), Cell(row, ContactWorkbookSchema.AccountLinkColumns, "ContactId"));
        Assert.Equal(string.Empty, Cell(row, ContactWorkbookSchema.AccountLinkColumns, "Operation"));
    }

    [Fact]
    public async Task Export_Excludes_Historically_Ended_Links_By_Default()
    {
        var handler = ExportHandler(out var contacts, out _, out var links, out _, out _);
        var contact = SeedContact(contacts, "Ada", "Lovelace");
        links.Items.Add(Link(contact.Id, "active"));
        links.Items.Add(Link(contact.Id, "ended", validTo: new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)));

        var res = await handler.Handle(new ExportContactsWorkbookQuery(new ContactWorkbookOptions(IncludeLinks: true)), default);

        var rows = DataRows(Open(res.Data!), ContactWorkbookSchema.AccountLinksSheet);
        Assert.Single(rows);
        Assert.Equal("active", Cell(rows[0], ContactWorkbookSchema.AccountLinkColumns, "Status"));
    }

    [Fact]
    public async Task Export_With_IncludeHistorical_Adds_Ended_Links_With_Status_And_ValidTo()
    {
        var handler = ExportHandler(out var contacts, out _, out var links, out _, out _);
        var contact = SeedContact(contacts, "Ada", "Lovelace");
        links.Items.Add(Link(contact.Id, "active"));
        links.Items.Add(Link(contact.Id, "ended", validTo: new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)));

        var res = await handler.Handle(
            new ExportContactsWorkbookQuery(new ContactWorkbookOptions(IncludeLinks: true, IncludeHistorical: true)), default);

        var rows = DataRows(Open(res.Data!), ContactWorkbookSchema.AccountLinksSheet);
        Assert.Equal(2, rows.Count);
        var ended = Assert.Single(rows, r => Cell(r, ContactWorkbookSchema.AccountLinkColumns, "Status") == "ended");
        Assert.Equal("2026-06-30", Cell(ended, ContactWorkbookSchema.AccountLinkColumns, "ValidTo"));
    }

    [Fact]
    public async Task Export_Leaves_Notes_Empty_Unless_They_Are_Explicitly_Requested()
    {
        var handler = ExportHandler(out var contacts, out _, out _, out _, out _);
        SeedContact(contacts, "Ada", "Lovelace").Notes = "internal remark";

        var res = await handler.Handle(new ExportContactsWorkbookQuery(new ContactWorkbookOptions()), default);

        var row = Assert.Single(DataRows(Open(res.Data!), ContactWorkbookSchema.ContactsSheet));
        Assert.Equal(string.Empty, Cell(row, ContactWorkbookSchema.ContactColumns, "Notes"));
    }

    [Fact]
    public async Task Export_With_IncludeNotes_Adds_Notes_And_CrossCountryReason()
    {
        var handler = ExportHandler(out var contacts, out _, out var links, out _, out _);
        var contact = SeedContact(contacts, "Ada", "Lovelace");
        contact.Notes = "internal remark";
        var link = Link(contact.Id, "active");
        link.Notes = "link note";
        link.CrossCountryReason = "regional coverage agreement";
        links.Items.Add(link);

        var res = await handler.Handle(
            new ExportContactsWorkbookQuery(new ContactWorkbookOptions(IncludeLinks: true, IncludeNotes: true)), default);

        using var wb = Open(res.Data!);
        Assert.Equal("internal remark", Cell(DataRows(wb, ContactWorkbookSchema.ContactsSheet)[0], ContactWorkbookSchema.ContactColumns, "Notes"));
        var linkRow = DataRows(wb, ContactWorkbookSchema.AccountLinksSheet)[0];
        Assert.Equal("regional coverage agreement", Cell(linkRow, ContactWorkbookSchema.AccountLinkColumns, "CrossCountryReason"));
    }

    [Fact]
    public async Task Export_Applies_ContactType_And_Status_Filters()
    {
        var handler = ExportHandler(out var contacts, out _, out _, out _, out _);
        SeedContact(contacts, "Ada", "Lovelace");
        var other = SeedContact(contacts, "Grace", "Hopper");
        other.ContactType = "pharmacist";

        var res = await handler.Handle(
            new ExportContactsWorkbookQuery(new ContactWorkbookOptions(ContactType: "pharmacist")), default);

        var row = Assert.Single(DataRows(Open(res.Data!), ContactWorkbookSchema.ContactsSheet));
        Assert.Equal("Grace", Cell(row, ContactWorkbookSchema.ContactColumns, "FirstName"));
    }

    [Fact]
    public async Task Export_Over_The_Row_Limit_Returns_A_Controlled_400_Asking_For_A_Filter()
    {
        var handler = ExportHandler(out var contacts, out _, out _, out _, out _);
        for (var i = 0; i <= ContactWorkbookSchema.MaxContactRows; i++)
        {
            SeedContact(contacts, "Contact", i.ToString());
        }

        var res = await handler.Handle(new ExportContactsWorkbookQuery(new ContactWorkbookOptions()), default);

        Assert.False(res.IsSuccessful);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains("filter", res.Errors![0], StringComparison.OrdinalIgnoreCase);
    }

    // ---------------- PII / audit ----------------

    [Fact]
    public async Task Export_Audit_Detail_Carries_Counts_And_Flags_But_No_Raw_Pii()
    {
        var handler = ExportHandler(out var contacts, out _, out _, out _, out var audit);
        var contact = SeedContact(contacts, "Ada", "Lovelace");
        contact.Email = "ada@example.com";
        contact.Phone = "+905321234567";
        contact.Notes = "internal remark";

        await handler.Handle(new ExportContactsWorkbookQuery(new ContactWorkbookOptions(IncludeNotes: true, ContactType: "doctor")), default);

        var detail = Assert.Single(audit.Details);
        Assert.Contains("count=1", detail);
        Assert.Contains("includeNotes=True", detail);
        Assert.Contains("filters=ContactType", detail);   // field NAME only, never the value
        Assert.DoesNotContain("Ada", detail);
        Assert.DoesNotContain("ada@example.com", detail);
        Assert.DoesNotContain("905321234567", detail);
        Assert.DoesNotContain("internal remark", detail);
        Assert.DoesNotContain("doctor", detail);
    }

    [Fact]
    public async Task Export_File_Name_Carries_No_Personal_Data()
    {
        var handler = ExportHandler(out var contacts, out _, out _, out _, out _);
        var contact = SeedContact(contacts, "Ada", "Lovelace");
        contact.Email = "ada@example.com";

        var res = await handler.Handle(new ExportContactsWorkbookQuery(new ContactWorkbookOptions()), default);

        Assert.StartsWith("contacts-export-", res.Data!.FileName);
        Assert.EndsWith(".xlsx", res.Data.FileName);
        Assert.DoesNotContain("Ada", res.Data.FileName);
        Assert.DoesNotContain("ada@example.com", res.Data.FileName);
    }

    [Fact]
    public async Task Template_Download_Is_Audited_Without_Contact_Data()
    {
        var handler = TemplateHandler(out _, out _, out var audit);

        await handler.Handle(new BuildContactTemplateWorkbookQuery(false), default);

        var detail = Assert.Single(audit.Details);
        Assert.Contains("format=xlsx", detail);
        Assert.DoesNotContain("count=", detail);
    }

    // ---------------- helpers ----------------

    private static BuildContactTemplateWorkbookHandler TemplateHandler(out FakeCatalog catalog, out FakeAccountRepo accounts)
        => TemplateHandler(out catalog, out accounts, out _);

    private static BuildContactTemplateWorkbookHandler TemplateHandler(
        out FakeCatalog catalog, out FakeAccountRepo accounts, out RecordingContactAudit audit)
    {
        catalog = new FakeCatalog();
        accounts = new FakeAccountRepo();
        audit = new RecordingContactAudit();
        return new BuildContactTemplateWorkbookHandler(Tenant(TenantA), catalog, accounts, audit);
    }

    private static ExportContactsWorkbookHandler ExportHandler(
        out FakeContactRepo contacts, out FakeContactRefRepo refs, out FakeLinkRepo links,
        out FakeAccountRepo accounts, out RecordingContactAudit audit)
    {
        contacts = new FakeContactRepo();
        refs = new FakeContactRefRepo();
        links = new FakeLinkRepo();
        accounts = new FakeAccountRepo();
        audit = new RecordingContactAudit();
        return new ExportContactsWorkbookHandler(Tenant(TenantA), contacts, refs, links, accounts, new FakeCatalog(), audit);
    }

    private static DomainContact SeedContact(FakeContactRepo repo, string first, string last)
    {
        var contact = new DomainContact
        {
            TenantId = TenantA,
            FirstName = first,
            LastName = last,
            DisplayName = $"{first} {last}",
            ContactType = "doctor",
            Status = "active"
        };
        repo.Items.Add(contact);
        return contact;
    }

    private static AccountContactLink Link(Guid contactId, string status, DateTimeOffset? validTo = null) => new()
    {
        TenantId = TenantA,
        AccountId = AccountA,
        ContactId = contactId,
        RoleCode = "decision-maker",
        Status = status,
        ValidTo = validTo
    };

    private static XLWorkbook Open(ExportFileDto file) => new(new MemoryStream(file.Content));

    private static IReadOnlyList<string> Header(XLWorkbook wb, string sheet)
    {
        var ws = wb.Worksheet(sheet);
        return ws.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
    }

    private static IReadOnlyList<IReadOnlyList<string>> DataRows(XLWorkbook wb, string sheet)
    {
        var ws = wb.Worksheet(sheet);
        var lastColumn = ws.Row(1).CellsUsed().Count();
        var rows = new List<IReadOnlyList<string>>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var values = Enumerable.Range(1, lastColumn).Select(c => ws.Cell(r, c).GetString()).ToList();
            if (values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                rows.Add(values);
            }
        }

        return rows;
    }

    private static string Cell(IReadOnlyList<string> row, IReadOnlyList<string> columns, string column)
        => row[ContactWorkbookSchema.ColumnIndex(columns, column) - 1];

    /// <summary>The in-cell dropdown source of a column's second row, or null when the column carries no validation.</summary>
    private static string? DataValidationFor(XLWorkbook wb, string sheet, IReadOnlyList<string> columns, string column)
    {
        var ws = wb.Worksheet(sheet);
        var cell = ws.Cell(2, ContactWorkbookSchema.ColumnIndex(columns, column));
        return cell.HasDataValidation ? cell.GetDataValidation().Value : null;
    }

    private static string SheetText(XLWorkbook wb, string sheet)
        => string.Join("\n", wb.Worksheet(sheet).CellsUsed().Select(c => c.GetString()));

    // ---------------- fakes ----------------

    /// <summary>MOD-0048 catalog seam. Nothing is published unless a test publishes it — proving the writer has no
    /// built-in value list to fall back on.</summary>
    private sealed class FakeCatalog : IReferenceDataCatalogReader
    {
        private readonly Dictionary<string, IReadOnlyList<ReferenceValueSnapshot>> _sets = new(StringComparer.OrdinalIgnoreCase);

        public void Publish(string setCode, params (string Code, string Label)[] values)
            => _sets[setCode] = values.Select(v => new ReferenceValueSnapshot(v.Code, v.Label, null, true, false, null)).ToList();

        public void PublishSnapshot(string setCode, params ReferenceValueSnapshot[] values) => _sets[setCode] = values;

        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken ct)
            => Task.FromResult(_sets.TryGetValue(setCode, out var values)
                ? new ReferenceSetSnapshot(setCode, true, values)
                : ReferenceSetSnapshot.NotPublished(setCode));
    }

    private sealed class RecordingContactAudit : IContactAuditPublisher
    {
        public List<string> Details { get; } = new();

        public Task PublishAsync(string eventName, Guid tenantId, Guid contactId, string? detail, CancellationToken ct)
        {
            Details.Add(detail ?? string.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeContactRepo : IContactRepository
    {
        public List<DomainContact> Items { get; } = new();
        public Task<DomainContact?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));
        public Task<IReadOnlyList<DomainContact>> ListByIdsAsync(Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult((IReadOnlyList<DomainContact>)Items.Where(c => c.TenantId == t && !c.IsDeleted && ids.Contains(c.Id)).ToList());
        public Task<(IReadOnlyList<DomainContact> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken ct) => Task.FromResult(((IReadOnlyList<DomainContact>)new List<DomainContact>(), 0L, 0L));
        public Task<IReadOnlyList<DomainContact>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<DomainContact>)Items.Where(c => c.TenantId == t && !c.IsDeleted).ToList());
        public Task InsertAsync(DomainContact c, CancellationToken ct) { Items.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(DomainContact c, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContactRefRepo : IContactExternalReferenceRepository
    {
        public List<ContactExternalReference> Items { get; } = new();
        public Task<bool> ExistsBySourceExternalAsync(Guid t, string s, string e, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<ContactExternalReference?> GetBySourceExternalAsync(Guid t, string s, string e, CancellationToken ct) => Task.FromResult<ContactExternalReference?>(null);
        public Task<IReadOnlyList<ContactExternalReference>> ListByContactAsync(Guid t, Guid c, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContactExternalReference>)Items.Where(r => r.ContactId == c).ToList());
        public Task<IReadOnlyList<ContactExternalReference>> ListAllAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContactExternalReference>)Items.Where(r => r.TenantId == t && !r.IsDeleted).ToList());
        public Task InsertAsync(ContactExternalReference r, CancellationToken ct) { Items.Add(r); return Task.CompletedTask; }
    }

    private sealed class FakeLinkRepo : IAccountContactLinkRepository
    {
        public List<AccountContactLink> Items { get; } = new();
        public Task<AccountContactLink?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(l => l.Id == id));
        public Task<bool> ExistsActiveAsync(Guid t, Guid a, Guid c, string r, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsPrimaryAsync(Guid t, Guid a, string r, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.AccountId == a).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid t, Guid c, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.ContactId == c).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted).ToList());
        public Task InsertAsync(AccountContactLink l, CancellationToken ct) { Items.Add(l); return Task.CompletedTask; }
        public Task UpdateAsync(AccountContactLink l, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public List<DomainAccount> Items { get; } = new();
        public Task<DomainAccount?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == t && a.Id == id && !a.IsDeleted));
        public Task<DomainAccount?> GetByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.AccountCode == code));
        public Task<bool> ExistsByCodeAsync(Guid t, string c, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<(IReadOnlyList<DomainAccount> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
            => Task.FromResult(((IReadOnlyList<DomainAccount>)Items.Where(a => a.TenantId == t && !a.IsDeleted).Take(ps).ToList(), (long)Items.Count, (long)Items.Count));
        public Task<IReadOnlyList<DomainAccount>> GetChildrenAsync(Guid t, Guid p, CancellationToken ct) => Task.FromResult((IReadOnlyList<DomainAccount>)new List<DomainAccount>());
        public Task<bool> WouldCreateCycleAsync(Guid t, Guid a, Guid c, CancellationToken ct) => Task.FromResult(false);
        public Task InsertAsync(DomainAccount a, CancellationToken ct) { Items.Add(a); return Task.CompletedTask; }
        public Task UpdateAsync(DomainAccount a, CancellationToken ct) => Task.CompletedTask;
    }
}

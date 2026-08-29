using ClosedXML.Excel;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.ImportExport;
using Diten.CrmService.Application.Features.ImportExport.Xlsx;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using DomainAccount = Diten.CrmService.Domain.Entities.Account;
using DomainContact = Diten.CrmService.Domain.Entities.Contact;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0150 Contact Import/Export Task 2 — XLSX upload, dry-run preview and safe apply.
/// Proves: nothing is written on a dry-run · identity-based matching (never e-mail/phone) · historical lifecycle
/// (end = Status ended + ValidTo, never delete, never overwrite) · cross-country parity with the UI write path ·
/// MOD-0048 reference parity · PII-safe messages · apply gates. AccountRelationship import is out of scope.
/// </summary>
public sealed class ContactWorkbookImportTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AccountA = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AccountB = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // ---------------- parser ----------------

    [Fact]
    public void Reader_Parses_Contacts_And_AccountLinks_Sheets()
    {
        var bytes = Workbook(
            contacts: new[] { Row(("Operation", "add"), ("FirstName", "Ada"), ("ContactType", "doctor"), ("ContactStatus", "active")) },
            links: new[] { Row(("Operation", "add"), ("AccountCode", "ACC-1"), ("RoleCode", "decision-maker")) });

        var parsed = ContactWorkbookReader.Read(new MemoryStream(bytes));

        Assert.True(parsed.IsReadable);
        Assert.Single(parsed.ContactRows);
        Assert.Single(parsed.LinkRows);
        Assert.Equal(2, parsed.ContactRows[0].RowNumber);      // real Excel row, header is row 1
        Assert.Equal("Ada", parsed.ContactRows[0].Get("FirstName"));
    }

    [Fact]
    public void Reader_Reports_A_Missing_Required_Column_As_A_File_Error()
    {
        var bytes = WorkbookWithHeader(ContactWorkbookSchema.ContactsSheet,
            new[] { "FirstName", "ContactType" },       // no Operation / ContactId
            new[] { new[] { "Ada", "doctor" } });

        var parsed = ContactWorkbookReader.Read(new MemoryStream(bytes));

        Assert.False(parsed.IsReadable);
        Assert.Contains(parsed.FileErrors, e => e.Contains("Operation"));
    }

    [Fact]
    public void Reader_Warns_About_Unknown_Columns_But_Still_Imports()
    {
        var bytes = WorkbookWithHeader(ContactWorkbookSchema.ContactsSheet,
            new[] { "Operation", "ContactId", "FirstName", "MyOwnNote" },
            new[] { new[] { "add", "", "Ada", "hello" } });

        var parsed = ContactWorkbookReader.Read(new MemoryStream(bytes));

        Assert.True(parsed.IsReadable);
        Assert.Single(parsed.ContactRows);
        Assert.Contains(parsed.FileWarnings, w => w.Contains("MyOwnNote"));
    }

    [Fact]
    public void Reader_Skips_Blank_Rows_And_Keeps_Text_Formatted_Values()
    {
        var bytes = WorkbookWithHeader(ContactWorkbookSchema.ContactsSheet,
            new[] { "Operation", "ContactId", "Phone", "PostalCode" },
            new[]
            {
                new[] { "add", "", "05321234567", "06010" },
                new[] { "", "", "", "" }
            });

        var parsed = ContactWorkbookReader.Read(new MemoryStream(bytes));

        var row = Assert.Single(parsed.ContactRows);
        Assert.Equal("05321234567", row.Get("Phone"));
        Assert.Equal("06010", row.Get("PostalCode"));
    }

    [Fact]
    public void Reader_Reads_A_Real_Excel_Date_Cell_As_Iso()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add(ContactWorkbookSchema.AccountLinksSheet);
        sheet.Cell(1, 1).Value = "Operation";
        sheet.Cell(1, 2).Value = "ValidTo";
        sheet.Cell(2, 1).Value = "end";
        sheet.Cell(2, 2).Value = new DateTime(2026, 6, 30);      // typed as a date, not text
        using var stream = new MemoryStream();
        wb.SaveAs(stream);

        var parsed = ContactWorkbookReader.Read(new MemoryStream(stream.ToArray()));

        Assert.Equal("2026-06-30", parsed.LinkRows[0].Get("ValidTo"));
    }

    [Fact]
    public void Reader_Rejects_A_File_That_Is_Not_A_Workbook()
    {
        var parsed = ContactWorkbookReader.Read(new MemoryStream(new byte[] { 1, 2, 3, 4 }));

        Assert.False(parsed.IsReadable);
        Assert.Single(parsed.FileErrors);
    }

    // ---------------- dry-run persists nothing ----------------

    [Fact]
    public async Task DryRun_Previews_A_Create_Without_Writing()
    {
        var ctx = new Ctx();
        var file = Workbook(contacts: new[] { NewContactRow("Ada", "Lovelace") });

        var res = await ctx.Handle(file, dryRun: true);

        var row = Assert.Single(res.Rows);
        Assert.Equal(ImportRowStatuses.Create, row.Status);
        Assert.Equal(1, res.Summary.Creates);
        Assert.True(res.CanApply);
        Assert.False(res.Applied);
        Assert.Empty(ctx.Contacts.Items);
    }

    [Fact]
    public async Task DryRun_Previews_An_End_Without_Closing_The_Link()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var link = ctx.SeedLink(contact.Id, AccountA);
        var file = Workbook(links: new[] { Row(("Operation", "end"), ("LinkId", link.Id.ToString()), ("ValidTo", "2026-06-30")) });

        var res = await ctx.Handle(file, dryRun: true);

        Assert.Equal(ImportRowStatuses.End, Assert.Single(res.Rows).Status);
        Assert.Equal("active", link.Status);
        Assert.Null(link.ValidTo);
    }

    // ---------------- apply: contacts ----------------

    [Fact]
    public async Task Apply_Creates_A_Contact_With_Derived_DisplayName_And_External_Reference()
    {
        var ctx = new Ctx();
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "add"), ("FirstName", "Ada"), ("LastName", "Lovelace"),
                ("ContactType", "doctor"), ("ContactStatus", "active"),
                ("ExternalSystem", "legacy-crm"), ("ExternalId", "EXT-1"), ("CountryCode", "tr"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.True(res.Applied);
        var created = Assert.Single(ctx.Contacts.Items);
        Assert.Equal("Ada Lovelace", created.DisplayName);
        Assert.Equal("tr", created.CountryRef);
        Assert.Single(ctx.Refs.Items);
    }

    [Fact]
    public async Task Apply_Updates_A_Contact_Matched_By_ContactId_And_Leaves_Blank_Cells_Untouched()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        contact.Phone = "+905321234567";
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "update"), ("ContactId", contact.Id.ToString()), ("Department", "cardiology"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        var row = Assert.Single(res.Rows);
        Assert.Equal(ImportRowStatuses.Update, row.Status);
        Assert.Contains("Department", row.ChangedFields);
        Assert.Equal("cardiology", contact.Department);
        Assert.Equal("+905321234567", contact.Phone);   // blank cell = leave unchanged
        Assert.Equal("Ada", contact.FirstName);
    }

    [Fact]
    public async Task Apply_Clears_A_Field_Only_With_The_Explicit_Clear_Token()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        contact.Department = "cardiology";
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "update"), ("ContactId", contact.Id.ToString()), ("Department", ImportValuesClearToken))
        });

        await ctx.Handle(file, dryRun: false);

        Assert.Null(contact.Department);
    }

    [Fact]
    public async Task Apply_Updates_A_Contact_Matched_By_External_Reference()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        ctx.Refs.Items.Add(new ContactExternalReference
        {
            TenantId = TenantA, ContactId = contact.Id, SourceSystem = "legacy-crm", ExternalId = "EXT-9"
        });
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "update"), ("ExternalSystem", "legacy-crm"), ("ExternalId", "EXT-9"), ("Specialty", "oncology"))
        });

        await ctx.Handle(file, dryRun: false);

        Assert.Equal("oncology", contact.Specialty);
    }

    [Fact]
    public async Task Update_Never_Matches_A_Contact_By_Email_Or_Phone()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        contact.Email = "ada@example.com";
        contact.Phone = "+905321234567";
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "update"), ("Email", "ada@example.com"), ("Phone", "+905321234567"), ("Department", "x"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        var row = Assert.Single(res.Rows);
        Assert.Equal(ImportRowStatuses.Error, row.Status);
        Assert.Equal("match_key_missing", row.Code);
        Assert.Null(contact.Department);
    }

    [Fact]
    public async Task Add_With_A_ContactId_Is_Rejected()
    {
        var ctx = new Ctx();
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "add"), ("ContactId", Guid.NewGuid().ToString()), ("FirstName", "Ada"),
                ("ContactType", "doctor"), ("ContactStatus", "active"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal("contact_id_on_add", Assert.Single(res.Rows).Code);
        Assert.Empty(ctx.Contacts.Items);
    }

    [Fact]
    public async Task Delete_Operation_Is_Not_Supported_On_Either_Sheet()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var link = ctx.SeedLink(contact.Id, AccountA);
        var file = Workbook(
            contacts: new[] { Row(("Operation", "delete"), ("ContactId", contact.Id.ToString())) },
            links: new[] { Row(("Operation", "delete"), ("LinkId", link.Id.ToString())) });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.All(res.Rows, r => Assert.Equal("unsupported_operation", r.Code));
        Assert.False(contact.IsDeleted);
        Assert.False(link.IsDeleted);
    }

    [Fact]
    public async Task A_Blank_Operation_Skips_The_Row_So_A_Plain_Export_Changes_Nothing()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", ""), ("ContactId", contact.Id.ToString()), ("FirstName", "Changed"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        var row = Assert.Single(res.Rows);
        Assert.Equal(ImportRowStatuses.Skip, row.Status);
        Assert.Equal("operation_missing", row.Code);
        Assert.Equal("Ada", contact.FirstName);
    }

    // ---------------- apply: account links ----------------

    [Fact]
    public async Task Apply_Adds_A_Link_Resolved_By_ContactId_And_AccountCode()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var file = Workbook(links: new[]
        {
            Row(("Operation", "add"), ("ContactId", contact.Id.ToString()), ("AccountCode", "ACC-1"),
                ("RoleCode", "decision-maker"), ("IsPrimary", "TRUE"), ("ValidFrom", "2026-07-01"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.True(res.Applied);
        var link = Assert.Single(ctx.Links.Items);
        Assert.Equal(AccountA, link.AccountId);
        Assert.True(link.IsPrimary);
        Assert.Equal("active", link.Status);
    }

    [Fact]
    public async Task A_Contact_Created_In_The_Same_File_Can_Be_Linked_By_Its_External_Id()
    {
        var ctx = new Ctx();
        var file = Workbook(
            contacts: new[]
            {
                Row(("Operation", "add"), ("FirstName", "Ada"), ("ContactType", "doctor"), ("ContactStatus", "active"),
                    ("ExternalSystem", "legacy-crm"), ("ExternalId", "EXT-1"))
            },
            links: new[]
            {
                Row(("Operation", "add"), ("ContactExternalSystem", "legacy-crm"), ("ContactExternalId", "EXT-1"),
                    ("AccountCode", "ACC-1"), ("RoleCode", "decision-maker"))
            });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(2, res.Summary.Creates);
        Assert.Single(ctx.Contacts.Items);
        Assert.Equal(ctx.Contacts.Items[0].Id, Assert.Single(ctx.Links.Items).ContactId);
    }

    [Fact]
    public async Task A_Link_Whose_Contact_Row_Failed_Is_Reported_As_A_Dependency_Skip()
    {
        var ctx = new Ctx();
        var file = Workbook(
            contacts: new[]
            {
                // no ContactType → the contact row fails
                Row(("Operation", "add"), ("FirstName", "Ada"), ("ContactStatus", "active"),
                    ("ExternalSystem", "legacy-crm"), ("ExternalId", "EXT-1"))
            },
            links: new[]
            {
                Row(("Operation", "add"), ("ContactExternalSystem", "legacy-crm"), ("ContactExternalId", "EXT-1"),
                    ("AccountCode", "ACC-1"), ("RoleCode", "decision-maker"))
            });

        var res = await ctx.Handle(file, dryRun: true);

        Assert.Contains(res.Rows, r => r.Status == ImportRowStatuses.Error && r.EntityType == "Contact");
        var linkRow = Assert.Single(res.Rows, r => r.EntityType == "AccountContactLink");
        Assert.Equal(ImportRowStatuses.SkippedDependency, linkRow.Status);
    }

    [Fact]
    public async Task Apply_Ends_A_Link_By_LinkId_Without_Deleting_It()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var link = ctx.SeedLink(contact.Id, AccountA);
        var file = Workbook(links: new[]
        {
            Row(("Operation", "end"), ("LinkId", link.Id.ToString()), ("ValidTo", "2026-06-30"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(1, res.Summary.Ends);
        Assert.Equal("ended", link.Status);
        Assert.Equal(new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero), link.ValidTo);
        Assert.False(link.IsDeleted);
        Assert.Single(ctx.Links.Items);        // the record is kept
    }

    [Fact]
    public async Task Ending_A_Link_Requires_An_End_Date()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var link = ctx.SeedLink(contact.Id, AccountA);
        var file = Workbook(links: new[] { Row(("Operation", "end"), ("LinkId", link.Id.ToString())) });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal("end_requires_validto", Assert.Single(res.Rows).Code);
        Assert.Equal("active", link.Status);
    }

    [Fact]
    public async Task Moving_A_Contact_Ends_The_Old_Link_And_Adds_A_New_One_In_The_Same_File()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ayse", "Yilmaz");
        var oldLink = ctx.SeedLink(contact.Id, AccountA);
        var file = Workbook(links: new[]
        {
            Row(("Operation", "end"), ("LinkId", oldLink.Id.ToString()), ("ValidTo", "2026-06-30")),
            Row(("Operation", "add"), ("ContactId", contact.Id.ToString()), ("AccountCode", "ACC-2"),
                ("RoleCode", "decision-maker"), ("ValidFrom", "2026-07-01"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(1, res.Summary.Ends);
        Assert.Equal(1, res.Summary.Creates);
        Assert.Equal(2, ctx.Links.Items.Count);                       // history preserved
        Assert.Equal("ended", oldLink.Status);
        Assert.False(oldLink.IsDeleted);
        var newLink = ctx.Links.Items.Single(l => l.Id != oldLink.Id);
        Assert.Equal(AccountB, newLink.AccountId);
        Assert.Equal("active", newLink.Status);
    }

    [Fact]
    public async Task An_Ended_Link_Does_Not_Block_A_New_Active_Link_With_The_Same_Key()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ayse", "Yilmaz");
        var ended = ctx.SeedLink(contact.Id, AccountA);
        ended.Status = "ended";
        ended.ValidTo = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var file = Workbook(links: new[]
        {
            Row(("Operation", "add"), ("ContactId", contact.Id.ToString()), ("AccountCode", "ACC-1"), ("RoleCode", "decision-maker"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(1, res.Summary.Creates);
        Assert.Equal(2, ctx.Links.Items.Count);
    }

    [Fact]
    public async Task A_Link_Update_Cannot_Repoint_The_Account()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var link = ctx.SeedLink(contact.Id, AccountA);
        var file = Workbook(links: new[]
        {
            Row(("Operation", "update"), ("LinkId", link.Id.ToString()), ("AccountId", AccountB.ToString()))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(ImportRowStatuses.Error, Assert.Single(res.Rows).Status);
        Assert.Equal(AccountA, link.AccountId);
    }

    // ---------------- validation parity ----------------

    [Fact]
    public async Task An_Invalid_Contact_Type_Fails_The_Row()
    {
        var ctx = new Ctx();
        ctx.Validator.Invalid.Add("nonsense");
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "add"), ("FirstName", "Ada"), ("ContactType", "nonsense"), ("ContactStatus", "active"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(ImportRowStatuses.Error, Assert.Single(res.Rows).Status);
        Assert.Empty(ctx.Contacts.Items);
    }

    [Fact]
    public async Task An_Unpublished_Required_Set_Blocks_The_Whole_Apply()
    {
        var ctx = new Ctx();
        ctx.Validator.MissingSets.Add(ContactReferenceValidation.ContactTypeSet);
        var file = Workbook(contacts: new[] { NewContactRow("Ada", "Lovelace") });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.False(res.CanApply);
        Assert.False(res.Applied);
        Assert.Contains("contact-type", res.BlockedReason);
        Assert.Empty(ctx.Contacts.Items);
    }

    [Fact]
    public async Task An_Invalid_Email_Fails_The_Row()
    {
        var ctx = new Ctx();
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "add"), ("FirstName", "Ada"), ("ContactType", "doctor"), ("ContactStatus", "active"),
                ("Email", "not-an-email"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(ImportRowStatuses.Error, Assert.Single(res.Rows).Status);
    }

    [Fact]
    public async Task A_Duplicate_External_Reference_Is_A_Conflict()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        ctx.Refs.Items.Add(new ContactExternalReference
        {
            TenantId = TenantA, ContactId = contact.Id, SourceSystem = "legacy-crm", ExternalId = "EXT-1"
        });
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "add"), ("FirstName", "Grace"), ("ContactType", "doctor"), ("ContactStatus", "active"),
                ("ExternalSystem", "legacy-crm"), ("ExternalId", "EXT-1"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(ImportRowStatuses.Conflict, Assert.Single(res.Rows).Status);
        Assert.Single(ctx.Contacts.Items);
    }

    [Fact]
    public async Task A_Duplicate_Active_Link_Is_A_Conflict()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        ctx.SeedLink(contact.Id, AccountA);
        var file = Workbook(links: new[]
        {
            Row(("Operation", "add"), ("ContactId", contact.Id.ToString()), ("AccountCode", "ACC-1"), ("RoleCode", "decision-maker"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal("duplicate_link", Assert.Single(res.Rows).Code);
        Assert.Single(ctx.Links.Items);
    }

    [Fact]
    public async Task A_Second_Primary_For_The_Same_Account_And_Role_Is_A_Conflict()
    {
        var ctx = new Ctx();
        var first = ctx.SeedContact("Ada", "Lovelace");
        var second = ctx.SeedContact("Grace", "Hopper");
        var link = ctx.SeedLink(first.Id, AccountA);
        link.IsPrimary = true;
        var file = Workbook(links: new[]
        {
            Row(("Operation", "add"), ("ContactId", second.Id.ToString()), ("AccountCode", "ACC-1"),
                ("RoleCode", "decision-maker"), ("IsPrimary", "TRUE"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal("second_primary", Assert.Single(res.Rows).Code);
    }

    [Fact]
    public async Task ValidFrom_After_ValidTo_Fails_The_Row()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var file = Workbook(links: new[]
        {
            Row(("Operation", "add"), ("ContactId", contact.Id.ToString()), ("AccountCode", "ACC-1"),
                ("RoleCode", "decision-maker"), ("ValidFrom", "2026-07-01"), ("ValidTo", "2026-06-01"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(ImportRowStatuses.Error, Assert.Single(res.Rows).Status);
    }

    // ---------------- cross-country parity ----------------

    [Fact]
    public async Task A_Cross_Country_Link_Without_A_Reason_Fails_On_Import_Too()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        contact.CountryRef = "tr";
        ctx.Accounts.Items.Single(a => a.Id == AccountA).CountryRef = "us";
        var file = Workbook(links: new[]
        {
            Row(("Operation", "add"), ("ContactId", contact.Id.ToString()), ("AccountCode", "ACC-1"), ("RoleCode", "decision-maker"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        var row = Assert.Single(res.Rows);
        Assert.Equal(ImportRowStatuses.Error, row.Status);
        Assert.Contains("different countries", row.Message);
        Assert.Empty(ctx.Links.Items);
    }

    [Fact]
    public async Task A_Cross_Country_Link_With_A_Reason_Is_Allowed_And_Stores_The_Reason()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        contact.CountryRef = "tr";
        ctx.Accounts.Items.Single(a => a.Id == AccountA).CountryRef = "us";
        var file = Workbook(links: new[]
        {
            Row(("Operation", "add"), ("ContactId", contact.Id.ToString()), ("AccountCode", "ACC-1"),
                ("RoleCode", "decision-maker"), ("CrossCountryReason", "regional coverage agreement"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(1, res.Summary.Creates);
        Assert.Equal("regional coverage agreement", Assert.Single(ctx.Links.Items).CrossCountryReason);
    }

    [Fact]
    public async Task An_Unknown_Country_Never_Blocks_A_Link()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");   // no country on either side
        var file = Workbook(links: new[]
        {
            Row(("Operation", "add"), ("ContactId", contact.Id.ToString()), ("AccountCode", "ACC-1"), ("RoleCode", "decision-maker"))
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.Equal(1, res.Summary.Creates);
    }

    // ---------------- permissions ----------------

    [Fact]
    public async Task Link_Rows_Fail_Closed_Without_The_Account_Contact_Manage_Permission()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var file = Workbook(links: new[]
        {
            Row(("Operation", "add"), ("ContactId", contact.Id.ToString()), ("AccountCode", "ACC-1"), ("RoleCode", "decision-maker"))
        });

        var res = await ctx.Handle(file, dryRun: false, capabilities: new ImportCapabilities(true, true, false));

        Assert.Equal("permission_denied", Assert.Single(res.Rows).Code);
        Assert.Empty(ctx.Links.Items);
    }

    [Fact]
    public async Task Contact_Update_Rows_Fail_Closed_Without_The_Update_Permission()
    {
        var ctx = new Ctx();
        var contact = ctx.SeedContact("Ada", "Lovelace");
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "update"), ("ContactId", contact.Id.ToString()), ("Department", "cardiology"))
        });

        var res = await ctx.Handle(file, dryRun: false, capabilities: new ImportCapabilities(true, false, true));

        Assert.Equal("permission_denied", Assert.Single(res.Rows).Code);
        Assert.Null(contact.Department);
    }

    // ---------------- apply gates ----------------

    [Fact]
    public async Task Strict_Mode_Applies_Nothing_When_Any_Row_Failed()
    {
        var ctx = new Ctx();
        var file = Workbook(contacts: new[]
        {
            NewContactRow("Ada", "Lovelace"),
            Row(("Operation", "add"), ("FirstName", "Broken"))     // no type/status
        });

        var res = await ctx.Handle(file, dryRun: false, strict: true);

        Assert.False(res.CanApply);
        Assert.False(res.Applied);
        Assert.Empty(ctx.Contacts.Items);
    }

    [Fact]
    public async Task Apply_Valid_Rows_Is_The_Default_Strategy()
    {
        var ctx = new Ctx();
        var file = Workbook(contacts: new[]
        {
            NewContactRow("Ada", "Lovelace"), NewContactRow("Grace", "Hopper"),
            NewContactRow("Katherine", "Johnson"), NewContactRow("Annie", "Easley"),
            NewContactRow("Mary", "Jackson"),
            Row(("Operation", "add"), ("FirstName", "Broken"))     // 1 of 6 → under the 20% gate
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.True(res.Applied);
        Assert.Equal(5, ctx.Contacts.Items.Count);
        Assert.Equal(1, res.Summary.Errors);
    }

    [Fact]
    public async Task Too_Many_Broken_Rows_Block_The_Apply()
    {
        var ctx = new Ctx();
        var file = Workbook(contacts: new[]
        {
            NewContactRow("Ada", "Lovelace"),
            Row(("Operation", "add"), ("FirstName", "Broken"))     // 1 of 2 = 50% > 20%
        });

        var res = await ctx.Handle(file, dryRun: false);

        Assert.False(res.CanApply);
        Assert.False(res.Applied);
        Assert.Contains("20%", res.BlockedReason);
        Assert.Empty(ctx.Contacts.Items);
    }

    // ---------------- PII ----------------

    [Fact]
    public async Task Preview_Rows_Never_Expose_Raw_Personal_Data()
    {
        var ctx = new Ctx();
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "add"), ("FirstName", "Ada"), ("LastName", "Lovelace"),
                ("ContactType", "doctor"), ("ContactStatus", "active"),
                ("Email", "not-an-email"), ("Phone", "+905321234567"), ("Notes", "sensitive remark"))
        });

        var res = await ctx.Handle(file, dryRun: true);

        var row = Assert.Single(res.Rows);
        Assert.DoesNotContain("Lovelace", row.Message);
        Assert.DoesNotContain("not-an-email", row.Message);
        Assert.DoesNotContain("905321234567", row.Message);
        Assert.DoesNotContain("sensitive remark", row.Message);
        Assert.Equal("A** L****", row.DisplayLabel);            // masked, still recognisable to the owner
    }

    [Fact]
    public async Task Import_Audit_Carries_Counts_Only()
    {
        var ctx = new Ctx();
        var file = Workbook(contacts: new[]
        {
            Row(("Operation", "add"), ("FirstName", "Ada"), ("LastName", "Lovelace"),
                ("ContactType", "doctor"), ("ContactStatus", "active"),
                ("Email", "ada@example.com"), ("Phone", "+905321234567"), ("Notes", "sensitive remark"))
        });

        await ctx.Handle(file, dryRun: false);

        Assert.All(ctx.Audit.Details, detail =>
        {
            Assert.DoesNotContain("Lovelace", detail);
            Assert.DoesNotContain("ada@example.com", detail);
            Assert.DoesNotContain("905321234567", detail);
            Assert.DoesNotContain("sensitive remark", detail);
        });
        Assert.Contains(ctx.Audit.Details, d => d.Contains("creates=1"));
    }

    // ---------------- helpers ----------------

    private const string ImportValuesClearToken = "<CLEAR>";

    private static (string, string)[] NewContactRow(string first, string last) => Row(
        ("Operation", "add"), ("FirstName", first), ("LastName", last),
        ("ContactType", "doctor"), ("ContactStatus", "active"));

    private static (string, string)[] Row(params (string Column, string Value)[] cells) => cells;

    private static byte[] Workbook((string, string)[][]? contacts = null, (string, string)[][]? links = null)
    {
        using var wb = new XLWorkbook();
        WriteSheet(wb, ContactWorkbookSchema.ContactsSheet, ContactWorkbookSchema.ContactColumns, contacts);
        WriteSheet(wb, ContactWorkbookSchema.AccountLinksSheet, ContactWorkbookSchema.AccountLinkColumns, links);
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSheet(XLWorkbook wb, string name, IReadOnlyList<string> columns, (string, string)[][]? rows)
    {
        var sheet = wb.Worksheets.Add(name);
        for (var c = 0; c < columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = columns[c];
        }

        if (rows is null)
        {
            return;
        }

        for (var r = 0; r < rows.Length; r++)
        {
            foreach (var (column, value) in rows[r])
            {
                var index = ContactWorkbookSchema.ColumnIndex(columns, column);
                if (index > 0)
                {
                    sheet.Cell(r + 2, index).SetValue(value);
                }
            }
        }
    }

    private static byte[] WorkbookWithHeader(string sheetName, string[] header, string[][] rows)
    {
        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add(sheetName);
        for (var c = 0; c < header.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = header[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).SetValue(rows[r][c]);
            }
        }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Handler + fakes wired together, with two seeded accounts (ACC-1 / ACC-2).</summary>
    private sealed class Ctx
    {
        public FakeContactRepo Contacts { get; } = new();
        public FakeContactRefRepo Refs { get; } = new();
        public FakeLinkRepo Links { get; } = new();
        public FakeAccountRepo Accounts { get; } = new();
        public FakeValidator Validator { get; } = new();
        public RecordingAudit Audit { get; } = new();

        public Ctx()
        {
            Accounts.Items.Add(new DomainAccount { Id = AccountA, TenantId = TenantA, AccountCode = "ACC-1", AccountName = "Hospital A" });
            Accounts.Items.Add(new DomainAccount { Id = AccountB, TenantId = TenantA, AccountCode = "ACC-2", AccountName = "Hospital B" });
        }

        public DomainContact SeedContact(string first, string last)
        {
            var contact = new DomainContact
            {
                TenantId = TenantA, FirstName = first, LastName = last, DisplayName = $"{first} {last}",
                ContactType = "doctor", Status = "active"
            };
            Contacts.Items.Add(contact);
            return contact;
        }

        public AccountContactLink SeedLink(Guid contactId, Guid accountId)
        {
            var link = new AccountContactLink
            {
                TenantId = TenantA, ContactId = contactId, AccountId = accountId,
                RoleCode = "decision-maker", Status = "active"
            };
            Links.Items.Add(link);
            return link;
        }

        public async Task<ImportPreviewDto> Handle(
            byte[] file, bool dryRun, bool strict = false, ImportCapabilities? capabilities = null)
        {
            var tenant = new TenantContext();
            tenant.SetTenant(TenantA);
            var handler = new ContactWorkbookImportHandler(tenant, Contacts, Refs, Links, Accounts, Validator, Audit);
            var res = await handler.Handle(
                new ImportContactWorkbookCommand(file, dryRun, strict, capabilities ?? ImportCapabilities.Full), default);
            Assert.True(res.IsSuccessful);
            return res.Data!;
        }
    }

    private sealed class FakeValidator : IReferenceDataValidator
    {
        public HashSet<string> Invalid { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> MissingSets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
        {
            var status = MissingSets.Contains(setCode)
                ? ReferenceValidationStatus.SetMissing
                : Invalid.Contains(value)
                    ? ReferenceValidationStatus.InvalidValue
                    : ReferenceValidationStatus.Valid;
            return Task.FromResult(new ReferenceValidationResult(status, setCode, value));
        }
    }

    private sealed class RecordingAudit : IContactAuditPublisher
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
        public Task<(IReadOnlyList<DomainContact> Items, long Total)> ListAsync(Guid t, string? s, int p, int ps, CancellationToken ct) => Task.FromResult(((IReadOnlyList<DomainContact>)Items, (long)Items.Count));
        public Task<IReadOnlyList<DomainContact>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<DomainContact>)Items.Where(c => c.TenantId == t && !c.IsDeleted).ToList());
        public Task InsertAsync(DomainContact c, CancellationToken ct) { Items.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(DomainContact c, CancellationToken ct) => Task.CompletedTask;   // entities are tracked in-place
    }

    private sealed class FakeContactRefRepo : IContactExternalReferenceRepository
    {
        public List<ContactExternalReference> Items { get; } = new();
        public Task<bool> ExistsBySourceExternalAsync(Guid t, string s, string e, Guid? ex, CancellationToken ct)
            => Task.FromResult(Items.Any(r => r.SourceSystem == s && r.ExternalId == e));
        public Task<ContactExternalReference?> GetBySourceExternalAsync(Guid t, string s, string e, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(r => r.SourceSystem == s && r.ExternalId == e));
        public Task<IReadOnlyList<ContactExternalReference>> ListByContactAsync(Guid t, Guid c, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContactExternalReference>)Items.Where(r => r.ContactId == c).ToList());
        public Task<IReadOnlyList<ContactExternalReference>> ListAllAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContactExternalReference>)Items.ToList());
        public Task InsertAsync(ContactExternalReference r, CancellationToken ct) { Items.Add(r); return Task.CompletedTask; }
    }

    private sealed class FakeLinkRepo : IAccountContactLinkRepository
    {
        public List<AccountContactLink> Items { get; } = new();
        private IEnumerable<AccountContactLink> Open => Items.Where(l => !l.IsDeleted && !RelationshipLifecycle.IsClosed(l.Status));
        public Task<AccountContactLink?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(l => l.Id == id && !l.IsDeleted));
        public Task<bool> ExistsActiveAsync(Guid t, Guid a, Guid c, string r, Guid? ex, CancellationToken ct)
            => Task.FromResult(Open.Any(l => l.AccountId == a && l.ContactId == c && string.Equals(l.RoleCode, r, StringComparison.OrdinalIgnoreCase) && l.Id != ex));
        public Task<bool> ExistsPrimaryAsync(Guid t, Guid a, string r, Guid? ex, CancellationToken ct)
            => Task.FromResult(Open.Any(l => l.AccountId == a && string.Equals(l.RoleCode, r, StringComparison.OrdinalIgnoreCase) && l.IsPrimary && l.Id != ex));
        public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.AccountId == a && !l.IsDeleted).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid t, Guid c, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.ContactId == c && !l.IsDeleted).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.ToList());
        public Task InsertAsync(AccountContactLink l, CancellationToken ct) { Items.Add(l); return Task.CompletedTask; }
        public Task UpdateAsync(AccountContactLink l, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public List<DomainAccount> Items { get; } = new();
        public Task<DomainAccount?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id && !a.IsDeleted));
        public Task<DomainAccount?> GetByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.AccountCode == code && !a.IsDeleted));
        public Task<bool> ExistsByCodeAsync(Guid t, string c, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<(IReadOnlyList<DomainAccount> Items, long Total)> ListAsync(Guid t, string? s, int p, int ps, CancellationToken ct)
            => Task.FromResult(((IReadOnlyList<DomainAccount>)Items, (long)Items.Count));
        public Task<IReadOnlyList<DomainAccount>> GetChildrenAsync(Guid t, Guid p, CancellationToken ct) => Task.FromResult((IReadOnlyList<DomainAccount>)new List<DomainAccount>());
        public Task<bool> WouldCreateCycleAsync(Guid t, Guid a, Guid c, CancellationToken ct) => Task.FromResult(false);
        public Task InsertAsync(DomainAccount a, CancellationToken ct) { Items.Add(a); return Task.CompletedTask; }
        public Task UpdateAsync(DomainAccount a, CancellationToken ct) => Task.CompletedTask;
    }
}

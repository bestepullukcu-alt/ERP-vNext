using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.ContactAvailability;
using Diten.CrmService.Application.Features.ContactAvailability.Commands;
using Diten.CrmService.Application.Features.ContactAvailability.Handlers;
using Diten.CrmService.Application.Features.ContactAvailability.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using DomainAvailability = Diten.CrmService.Domain.Entities.ContactAvailability;
using DomainException = Diten.CrmService.Domain.Entities.ContactAvailabilityException;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0150 FU07 — AccountContactLink-scoped contact availability / visit preference.
/// The rules these tests pin down: availability is link-scoped (never a Contact field), two links carry two
/// independent schedules, overlaps are a controlled 409 that names both rows, an identical repost is idempotent,
/// a date exception overrides the weekly pattern, and MISSING data is "unknown" — never "unavailable".
/// </summary>
public sealed class ContactAvailabilityTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string Type = "working-hours";
    private const string Source = "manual";

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeAccountRepo Accounts { get; } = new();
        public FakeContactRepo Contacts { get; } = new();
        public FakeLinkRepo Links { get; } = new();
        public FakeAvailabilityRepo Availability { get; } = new();
        public FakeExceptionRepo Exceptions { get; } = new();
        public FakeValidator Validator { get; } = new();

        public Guid TenantId { get; }
        public Contact Contact { get; }
        public Account Hospital { get; }
        public Account Clinic { get; }
        public AccountContactLink HospitalLink { get; }
        public AccountContactLink ClinicLink { get; }

        public Fixture(Guid tenant)
        {
            TenantId = tenant;
            Contact = new Contact { TenantId = tenant, FirstName = "Ayse", LastName = "K", DisplayName = "Dr Ayse", ContactType = "doctor", Status = "active" };
            Hospital = new Account { TenantId = tenant, AccountName = "Medicana Beylikduzu", AccountCode = "ACC-1", AccountType = "hospital", Status = "active" };
            Clinic = new Account { TenantId = tenant, AccountName = "Klinik X", AccountCode = "ACC-2", AccountType = "clinic", Status = "active" };
            Contacts.Items.Add(Contact);
            Accounts.Items.Add(Hospital);
            Accounts.Items.Add(Clinic);

            HospitalLink = NewLink(Hospital.Id);
            ClinicLink = NewLink(Clinic.Id);
            Links.Items.Add(HospitalLink);
            Links.Items.Add(ClinicLink);
        }

        private AccountContactLink NewLink(Guid accountId) => new()
        {
            TenantId = TenantId,
            AccountId = accountId,
            ContactId = Contact.Id,
            RoleCode = "decision-maker",
            Status = "active"
        };

        public CreateContactAvailabilityHandler Create(Guid? tenant = null) =>
            new(Tenant(tenant ?? TenantId), new NullActorContext(), Links, Availability, Validator, new NoopAudit());

        public UpdateContactAvailabilityHandler Update(Guid? tenant = null) =>
            new(Tenant(tenant ?? TenantId), new NullActorContext(), Availability, Validator, new NoopAudit());

        public DeactivateContactAvailabilityHandler Deactivate() =>
            new(Tenant(TenantId), new NullActorContext(), Availability, new NoopAudit());

        public ArchiveContactAvailabilityHandler Archive() =>
            new(Tenant(TenantId), new NullActorContext(), Availability, new NoopAudit());

        public CreateContactAvailabilityExceptionHandler CreateException(Guid? tenant = null) =>
            new(Tenant(tenant ?? TenantId), new NullActorContext(), Links, Exceptions, Validator, new NoopAudit());

        public UpdateContactAvailabilityExceptionHandler UpdateException() =>
            new(Tenant(TenantId), new NullActorContext(), Exceptions, Validator, new NoopAudit());

        public DeactivateContactAvailabilityExceptionHandler DeactivateException() =>
            new(Tenant(TenantId), new NullActorContext(), Exceptions, new NoopAudit());

        public LookupContactAvailabilityHandler Lookup(Guid? tenant = null) =>
            new(Tenant(tenant ?? TenantId), Links, Availability, Exceptions, Contacts, Accounts);

        public ListContactAvailabilityHandler ListForContact() =>
            new(Tenant(TenantId), Contacts, Links, Availability, Exceptions, Accounts);

        public GetLinkAvailabilityHandler GetLink() =>
            new(Tenant(TenantId), Links, Availability, Exceptions, Contacts, Accounts);

        public ListAccountContactAvailabilityHandler ListForAccount() =>
            new(Tenant(TenantId), Accounts, Links, Availability, Exceptions, Contacts);
    }

    private static CreateContactAvailabilityCommand Cmd(
        Guid linkId,
        string weekday = "monday",
        string start = "09:00",
        string end = "13:00",
        VisitPreferenceInput? preference = null,
        int? averageDuration = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? status = null)
        => new(linkId, weekday, start, end, Type, Source, status, preference, averageDuration, from, to, null);

    // ---------------- Ownership: link-scoped, never a Contact field ----------------

    [Fact]
    public async Task Create_Success_Derives_Contact_And_Account_From_Link()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id), default);

        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Availability.Items);
        Assert.Equal(f.HospitalLink.Id, row.AccountContactLinkId);
        // Derived from the link — the payload has no contact/account field at all.
        Assert.Equal(f.Contact.Id, row.ContactId);
        Assert.Equal(f.Hospital.Id, row.AccountId);
        Assert.Equal("monday", row.Weekday);
    }

    [Fact]
    public async Task Create_Unknown_Link_Returns_404()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(Guid.NewGuid()), default);
        Assert.Equal(404, r.StatusCode);
        Assert.Empty(f.Availability.Items);
    }

    [Fact]
    public async Task Create_CrossTenant_Link_Returns_404()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create(TenantB).Handle(Cmd(f.HospitalLink.Id), default);
        Assert.Equal(404, r.StatusCode);
        Assert.Empty(f.Availability.Items);
    }

    [Fact]
    public async Task Two_Links_Of_Same_Contact_Hold_Independent_Schedules()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00"), default);
        await f.Create().Handle(Cmd(f.ClinicLink.Id, "tuesday", "10:00", "16:00"), default);

        var hospital = await f.GetLink().Handle(new GetLinkAvailabilityQuery(f.HospitalLink.Id), default);
        var clinic = await f.GetLink().Handle(new GetLinkAvailabilityQuery(f.ClinicLink.Id), default);

        Assert.Single(hospital.Data!.Availability);
        Assert.Single(clinic.Data!.Availability);
        Assert.Equal("monday", hospital.Data!.Availability[0].Weekday);
        Assert.Equal("tuesday", clinic.Data!.Availability[0].Weekday);
    }

    // ---------------- Validation ----------------

    [Fact]
    public async Task Create_Start_Not_Before_End_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id, start: "13:00", end: "09:00"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Invalid_Weekday_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id, weekday: "pazartesi"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Preferred_Outside_Available_Returns_400()
    {
        var f = new Fixture(TenantA);
        var preference = new VisitPreferenceInput(PreferredVisitStartTime: "08:00", PreferredVisitEndTime: "10:00");
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id, preference: preference), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Preferred_Inside_Available_Succeeds()
    {
        var f = new Fixture(TenantA);
        var preference = new VisitPreferenceInput(PreferredVisitStartTime: "10:00", PreferredVisitEndTime: "12:00");
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id, preference: preference), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Create_Avoid_Window_May_Overlap_Available_Window()
    {
        // The avoid window is a STRONGER constraint inside the available window, not the inverse of preferred.
        var f = new Fixture(TenantA);
        var preference = new VisitPreferenceInput(AvoidVisitStartTime: "12:00", AvoidVisitEndTime: "13:00");
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id, preference: preference), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Create_EffectiveTo_Before_EffectiveFrom_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(
            f.HospitalLink.Id,
            from: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            to: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_On_Inactive_Link_Returns_400()
    {
        var f = new Fixture(TenantA);
        f.HospitalLink.Status = "ended";
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(f.Availability.Items);
    }

    [Fact]
    public async Task Create_Unpublished_Reference_Set_Is_FailClosed_400()
    {
        var f = new Fixture(TenantA);
        f.Validator.Status[ContactAvailabilityReferenceSets.Type] = ReferenceValidationStatus.SetMissing;
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(f.Availability.Items);
    }

    [Fact]
    public async Task Create_Invalid_Source_Value_Returns_400()
    {
        var f = new Fixture(TenantA);
        f.Validator.Status[ContactAvailabilityReferenceSets.Source] = ReferenceValidationStatus.InvalidValue;
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Overlapping_Window_Returns_409_Naming_Both_Rows()
    {
        var f = new Fixture(TenantA);
        var first = await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00"), default);
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "12:00", "15:00"), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Single(f.Availability.Items);
        var message = Assert.Single(r.Errors!);
        Assert.Contains(first.Data.ToString(), message); // existing row identity is reported — no silent merge
        Assert.Contains("12:00-15:00", message);         // and the requested one
    }

    [Fact]
    public async Task Create_NonOverlapping_Window_Same_Weekday_Succeeds()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00"), default);
        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "14:00", "17:00"), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal(2, f.Availability.Items.Count);
    }

    [Fact]
    public async Task Create_Identical_Row_Is_Idempotent_NoOp()
    {
        var f = new Fixture(TenantA);
        var first = await f.Create().Handle(Cmd(f.HospitalLink.Id), default);
        var second = await f.Create().Handle(Cmd(f.HospitalLink.Id), default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, second.StatusCode);       // no-op, not a duplicate and not an error
        Assert.Equal(first.Data, second.Data);
        Assert.Single(f.Availability.Items);
    }

    [Fact]
    public async Task Overlap_Does_Not_Fire_Against_Closed_Rows()
    {
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00"), default);
        await f.Deactivate().Handle(new DeactivateContactAvailabilityCommand(created.Data), default);

        var r = await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "10:00", "12:00"), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Update_Unknown_Row_Returns_404()
    {
        var f = new Fixture(TenantA);
        var r = await f.Update().Handle(
            new UpdateContactAvailabilityCommand(Guid.NewGuid(), "monday", "09:00", "13:00", Type, Source), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Update_Into_Overlap_Returns_409()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "12:00"), default);
        var second = await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "13:00", "17:00"), default);

        var r = await f.Update().Handle(
            new UpdateContactAvailabilityCommand(second.Data, "monday", "11:00", "17:00", Type, Source), default);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Update_Keeps_Owning_Link_And_Derived_Ids()
    {
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(Cmd(f.HospitalLink.Id), default);
        await f.Update().Handle(
            new UpdateContactAvailabilityCommand(created.Data, "tuesday", "10:00", "16:00", Type, Source), default);

        var row = Assert.Single(f.Availability.Items);
        Assert.Equal(f.HospitalLink.Id, row.AccountContactLinkId);
        Assert.Equal(f.Contact.Id, row.ContactId);
        Assert.Equal(f.Hospital.Id, row.AccountId);
        Assert.Equal("tuesday", row.Weekday);
    }

    // ---------------- Lifecycle: no hard delete ----------------

    [Fact]
    public async Task Deactivate_And_Archive_Keep_The_Row_Readable()
    {
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(Cmd(f.HospitalLink.Id), default);

        await f.Deactivate().Handle(new DeactivateContactAvailabilityCommand(created.Data), default);
        Assert.Equal(AvailabilityLifecycle.Inactive, f.Availability.Items[0].Status);

        await f.Archive().Handle(new ArchiveContactAvailabilityCommand(created.Data), default);
        Assert.Equal(AvailabilityLifecycle.Archived, f.Availability.Items[0].Status);

        // Still present and still readable — nothing was deleted.
        Assert.Single(f.Availability.Items);
        Assert.False(f.Availability.Items[0].IsDeleted);
    }

    [Fact]
    public void Availability_Repository_Exposes_No_Delete_Method()
    {
        // Guard: a hard-delete path must not exist even by accident.
        var names = typeof(IContactAvailabilityRepository).GetMethods().Select(m => m.Name).ToList();
        Assert.DoesNotContain(names, n => n.Contains("Delete", StringComparison.OrdinalIgnoreCase));

        var exceptionNames = typeof(IContactAvailabilityExceptionRepository).GetMethods().Select(m => m.Name).ToList();
        Assert.DoesNotContain(exceptionNames, n => n.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Update_With_Unsupported_Status_Returns_400()
    {
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(Cmd(f.HospitalLink.Id), default);
        var r = await f.Update().Handle(
            new UpdateContactAvailabilityCommand(created.Data, "monday", "09:00", "13:00", Type, Source, Status: "deleted"), default);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- Contact / Account master untouched ----------------

    [Fact]
    public async Task Writes_Never_Mutate_Contact_Or_Account_Master()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id), default);
        await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "2026-09-12", false, Source, Reason: "congress"), default);

        Assert.Equal(0, f.Contacts.UpdateCalls);
        Assert.Equal(0, f.Accounts.UpdateCalls);
        Assert.Equal(0, f.Links.UpdateCalls);
    }

    [Fact]
    public void Contact_Master_Has_No_Availability_Field()
    {
        // Availability must never collapse onto the Contact aggregate (multi-location doctors).
        var contactMembers = typeof(Contact).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(contactMembers, n =>
            n.Contains("Availability", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Weekday", StringComparison.OrdinalIgnoreCase)
            || n.Contains("VisitPreference", StringComparison.OrdinalIgnoreCase));
    }

    // ---------------- Exceptions ----------------

    [Fact]
    public async Task CreateException_Success()
    {
        var f = new Fixture(TenantA);
        var r = await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "2026-09-12", false, Source, Reason: "congress"), default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Exceptions.Items);
        Assert.Equal(f.Contact.Id, row.ContactId);
        Assert.Equal(f.Hospital.Id, row.AccountId);
        Assert.Equal("2026-09-12", row.Date);
        Assert.False(row.IsAvailable);
    }

    [Fact]
    public async Task CreateException_Duplicate_Active_Same_Date_Returns_409()
    {
        var f = new Fixture(TenantA);
        await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "2026-09-12", false, Source), default);
        var r = await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "2026-09-12", true, Source, "09:00", "11:00"), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Single(f.Exceptions.Items);
    }

    [Fact]
    public async Task CreateException_Invalid_Date_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "12.09.2026-not-a-date", false, Source), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task CreateException_Available_With_Broken_Window_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "2026-09-12", true, Source, "16:00", "09:00"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Deactivated_Exception_Is_Ignored_By_Lookup()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "saturday", "09:00", "13:00"), default);
        var ex = await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "2026-09-12", false, Source), default); // 2026-09-12 is a Saturday
        await f.DeactivateException().Handle(new DeactivateContactAvailabilityExceptionCommand(ex.Data), default);

        var lookup = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-09-12", AccountContactLinkId: f.HospitalLink.Id), default);

        var row = Assert.Single(lookup.Data!.Rows);
        Assert.False(row.ExceptionApplied);
        Assert.Equal(AvailabilityLookupStatus.Available, row.AvailabilityStatus);
    }

    // ---------------- Lookup ----------------

    [Fact]
    public async Task Lookup_Returns_Window_For_The_Weekday()
    {
        var f = new Fixture(TenantA);
        var preference = new VisitPreferenceInput(
            PreferredVisitStartTime: "10:00", PreferredVisitEndTime: "12:00", AppointmentRequired: true, AppointmentLeadTimeDays: 3);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00", preference, averageDuration: 20), default);

        // 2026-08-03 is a Monday.
        var lookup = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-08-03", ContactId: f.Contact.Id, AccountId: f.Hospital.Id), default);

        Assert.Equal("monday", lookup.Data!.Weekday);
        var row = Assert.Single(lookup.Data.Rows);
        Assert.Equal(AvailabilityLookupStatus.Available, row.AvailabilityStatus);
        Assert.Equal("09:00-13:00", row.AvailableWindow);
        Assert.Equal("10:00-12:00", row.PreferredWindow);
        Assert.True(row.AppointmentRequired);
        Assert.Equal(3, row.AppointmentLeadTimeDays);
        Assert.Equal(20, row.AverageVisitDurationMinutes);
        Assert.Contains(AvailabilityReasonCodes.AvailabilityOk, row.ReasonCodes);
        // AppointmentRequired is a WARNING, not a filter — the row stays available.
        Assert.Contains(AvailabilityReasonCodes.AppointmentRequired, row.ReasonCodes);
        Assert.Contains(AvailabilityReasonCodes.PreferredWindowDefined, row.ReasonCodes);
    }

    [Fact]
    public async Task Lookup_Avoid_Window_Is_Reported_As_Reason()
    {
        var f = new Fixture(TenantA);
        var preference = new VisitPreferenceInput(AvoidVisitStartTime: "12:00", AvoidVisitEndTime: "13:00");
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00", preference), default);

        var lookup = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-08-03", AccountContactLinkId: f.HospitalLink.Id), default);

        var row = Assert.Single(lookup.Data!.Rows);
        Assert.Equal("12:00-13:00", row.AvoidWindow);
        Assert.Contains(AvailabilityReasonCodes.AvoidWindowDefined, row.ReasonCodes);
        Assert.Equal(AvailabilityLookupStatus.Available, row.AvailabilityStatus);
    }

    [Fact]
    public async Task Lookup_Without_Any_Availability_Returns_Unknown_Not_Unavailable()
    {
        var f = new Fixture(TenantA);
        var lookup = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-08-03", ContactId: f.Contact.Id), default);

        Assert.All(lookup.Data!.Rows, row =>
        {
            Assert.Equal(AvailabilityLookupStatus.Unknown, row.AvailabilityStatus);
            Assert.Contains(AvailabilityReasonCodes.NoAvailabilityData, row.ReasonCodes);
            Assert.DoesNotContain(AvailabilityReasonCodes.NotAvailableOnDay, row.ReasonCodes);
        });
    }

    [Fact]
    public async Task Lookup_Other_Weekday_Returns_Unknown_Not_Unavailable()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00"), default);

        // 2026-08-04 is a Tuesday.
        var lookup = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-08-04", AccountContactLinkId: f.HospitalLink.Id), default);

        var row = Assert.Single(lookup.Data!.Rows);
        Assert.Equal(AvailabilityLookupStatus.Unknown, row.AvailabilityStatus);
        Assert.Contains(AvailabilityReasonCodes.NoAvailabilityData, row.ReasonCodes);
        Assert.DoesNotContain(AvailabilityReasonCodes.NotAvailableOnDay, row.ReasonCodes);
    }

    [Fact]
    public async Task Lookup_Exception_Overrides_Weekly_Pattern()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "saturday", "09:00", "13:00"), default);
        await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "2026-09-12", false, Source, Reason: "congress"), default);

        var lookup = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-09-12", AccountContactLinkId: f.HospitalLink.Id), default);

        var row = Assert.Single(lookup.Data!.Rows);
        Assert.Equal(AvailabilityLookupStatus.Unavailable, row.AvailabilityStatus);
        Assert.True(row.ExceptionApplied);
        Assert.Equal("congress", row.ExceptionReason);
        Assert.Contains(AvailabilityReasonCodes.ExceptionUnavailable, row.ReasonCodes);
        Assert.Null(row.AvailableWindow);
    }

    [Fact]
    public async Task Lookup_AdHoc_Exception_Window_Wins_Over_Weekly_Window()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "saturday", "09:00", "13:00"), default);
        await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "2026-09-12", true, Source, "15:00", "17:00"), default);

        var lookup = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-09-12", AccountContactLinkId: f.HospitalLink.Id), default);

        var row = Assert.Single(lookup.Data!.Rows);
        Assert.Equal(AvailabilityLookupStatus.Available, row.AvailabilityStatus);
        Assert.True(row.ExceptionApplied);
        Assert.Equal("15:00-17:00", row.AvailableWindow);
        Assert.Contains(AvailabilityReasonCodes.ExceptionWindowApplied, row.ReasonCodes);
    }

    [Fact]
    public async Task Lookup_Respects_Effective_Dates()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(
            f.HospitalLink.Id, "monday", "09:00", "13:00",
            from: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)), default);

        var before = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-08-03", AccountContactLinkId: f.HospitalLink.Id), default);
        var after = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-09-07", AccountContactLinkId: f.HospitalLink.Id), default);

        Assert.Equal(AvailabilityLookupStatus.Unavailable, before.Data!.Rows[0].AvailabilityStatus);
        Assert.Contains(AvailabilityReasonCodes.OutsideEffectiveWindow, before.Data.Rows[0].ReasonCodes);
        Assert.Equal(AvailabilityLookupStatus.Available, after.Data!.Rows[0].AvailabilityStatus);
    }

    [Fact]
    public async Task Lookup_Closed_Availability_Only_Is_Unknown()
    {
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00"), default);
        await f.Archive().Handle(new ArchiveContactAvailabilityCommand(created.Data), default);

        var lookup = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-08-03", AccountContactLinkId: f.HospitalLink.Id), default);

        var row = Assert.Single(lookup.Data!.Rows);
        Assert.Equal(AvailabilityLookupStatus.Unknown, row.AvailabilityStatus);
        Assert.Contains(AvailabilityReasonCodes.AvailabilityInactive, row.ReasonCodes);
    }

    [Fact]
    public async Task Lookup_Invalid_Date_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Lookup().Handle(new LookupContactAvailabilityQuery("not-a-date", ContactId: f.Contact.Id), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Lookup_Without_Any_Filter_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Lookup().Handle(new LookupContactAvailabilityQuery("2026-08-03"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Lookup_CrossTenant_Returns_No_Rows()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id), default);

        var r = await f.Lookup(TenantB).Handle(
            new LookupContactAvailabilityQuery("2026-08-03", AccountContactLinkId: f.HospitalLink.Id), default);

        Assert.True(r.IsSuccessful);
        Assert.Empty(r.Data!.Rows);
    }

    [Fact]
    public async Task Lookup_Contact_With_Two_Accounts_Returns_A_Row_Per_Link()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00"), default);
        await f.Create().Handle(Cmd(f.ClinicLink.Id, "monday", "14:00", "18:00"), default);

        var lookup = await f.Lookup().Handle(
            new LookupContactAvailabilityQuery("2026-08-03", ContactId: f.Contact.Id), default);

        Assert.Equal(2, lookup.Data!.Rows.Count);
        Assert.Contains(lookup.Data.Rows, r => r.AccountId == f.Hospital.Id && r.AvailableWindow == "09:00-13:00");
        Assert.Contains(lookup.Data.Rows, r => r.AccountId == f.Clinic.Id && r.AvailableWindow == "14:00-18:00");
    }

    [Fact]
    public void Lookup_Row_Carries_No_Route_Or_Plan_Field()
    {
        // Guard: the readiness seam must never grow an ordering/score/plan field. "AverageVisitDurationMinutes" is
        // fine — it is how long a visit takes here (availability data), not a plan.
        var members = typeof(ContactAvailabilityLookupRowDto).GetProperties().Select(p => p.Name).ToList();
        foreach (var forbidden in new[]
                 {
                     "Route", "Sequence", "Order", "Rank", "Distance", "Travel", "Score", "Priority",
                     "Plan", "VisitPlan", "Frequency", "Cadence", "Territory", "LastVisit", "Due"
                 })
        {
            Assert.DoesNotContain(members, m => m.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---------------- Read projections ----------------

    [Fact]
    public async Task ListForContact_Unknown_Contact_Returns_404()
    {
        var f = new Fixture(TenantA);
        var r = await f.ListForContact().Handle(new ListContactAvailabilityQuery(Guid.NewGuid()), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task ListForAccount_Returns_Rows_Of_That_Location_Only()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id, "monday", "09:00", "13:00"), default);
        await f.Create().Handle(Cmd(f.ClinicLink.Id, "tuesday", "10:00", "16:00"), default);

        var r = await f.ListForAccount().Handle(new ListAccountContactAvailabilityQuery(f.Hospital.Id), default);

        var link = Assert.Single(r.Data!);
        var row = Assert.Single(link.Availability);
        Assert.Equal("monday", row.Weekday);
        Assert.Equal(f.Hospital.Id, row.AccountId);
    }

    [Fact]
    public async Task GetLink_Includes_Exceptions_And_Link_State()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(f.HospitalLink.Id), default);
        await f.CreateException().Handle(
            new CreateContactAvailabilityExceptionCommand(f.HospitalLink.Id, "2026-09-12", false, Source), default);

        var r = await f.GetLink().Handle(new GetLinkAvailabilityQuery(f.HospitalLink.Id), default);

        Assert.True(r.Data!.LinkIsActive);
        Assert.Single(r.Data.Availability);
        Assert.Single(r.Data.Exceptions);
        Assert.Equal("Dr Ayse", r.Data.ContactDisplayName);
        Assert.Equal("Medicana Beylikduzu", r.Data.AccountDisplayName);
    }

    // ---------------- Permissions / contract shape ----------------

    [Fact]
    public void Permission_Keys_Are_Canonical_And_There_Is_No_Delete_Key()
    {
        Assert.Equal("crm.contact.availability.read", ContactAvailabilityPermissions.Read);
        Assert.Equal("crm.contact.availability.manage", ContactAvailabilityPermissions.Manage);
        // Documented fallback while the RBAC catalog does not carry the canonical keys.
        Assert.Equal("crm.contact.read", ContactAvailabilityPermissions.ReadFallback);
        Assert.Equal("crm.contact.update", ContactAvailabilityPermissions.ManageFallback);

        var keys = typeof(ContactAvailabilityPermissions).GetFields().Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.DoesNotContain(keys, k => k.Contains("delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Contract_Flags_Cover_Availability_Only()
    {
        var flags = typeof(Features.Contact.Contract.ContactFeatureFlags).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains("SupportsContactAvailability", flags);
        Assert.Contains("SupportsAccountContactLinkAvailability", flags);
        Assert.Contains("SupportsVisitPreference", flags);
        Assert.Contains("SupportsAvailabilityExceptions", flags);

        // Availability master data must never be advertised as planning capability.
        Assert.DoesNotContain(flags, f => f.Contains("VisitPlanning", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(flags, f => f.Contains("RoutePlanning", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(flags, f => f.Contains("VisitFrequency", StringComparison.OrdinalIgnoreCase));
    }

    // ---------------- Fakes ----------------

    private sealed class FakeAvailabilityRepo : IContactAvailabilityRepository
    {
        public List<DomainAvailability> Items { get; } = new();

        public Task<DomainAvailability?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == t && a.Id == id && !a.IsDeleted));

        public Task<IReadOnlyList<DomainAvailability>> ListByLinkAsync(Guid t, Guid linkId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<DomainAvailability>)Items.Where(a => a.TenantId == t && !a.IsDeleted && a.AccountContactLinkId == linkId).ToList());

        public Task<IReadOnlyList<DomainAvailability>> ListByContactAsync(Guid t, Guid contactId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<DomainAvailability>)Items.Where(a => a.TenantId == t && !a.IsDeleted && a.ContactId == contactId).ToList());

        public Task<IReadOnlyList<DomainAvailability>> ListByAccountAsync(Guid t, Guid accountId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<DomainAvailability>)Items.Where(a => a.TenantId == t && !a.IsDeleted && a.AccountId == accountId).ToList());

        public Task InsertAsync(DomainAvailability a, CancellationToken ct)
        {
            Items.Add(a);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DomainAvailability a, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeExceptionRepo : IContactAvailabilityExceptionRepository
    {
        public List<DomainException> Items { get; } = new();

        public Task<DomainException?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(e => e.TenantId == t && e.Id == id && !e.IsDeleted));

        public Task<IReadOnlyList<DomainException>> ListByLinkAsync(Guid t, Guid linkId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<DomainException>)Items.Where(e => e.TenantId == t && !e.IsDeleted && e.AccountContactLinkId == linkId).ToList());

        public Task<IReadOnlyList<DomainException>> ListByContactAsync(Guid t, Guid contactId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<DomainException>)Items.Where(e => e.TenantId == t && !e.IsDeleted && e.ContactId == contactId).ToList());

        public Task<IReadOnlyList<DomainException>> ListByAccountAsync(Guid t, Guid accountId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<DomainException>)Items.Where(e => e.TenantId == t && !e.IsDeleted && e.AccountId == accountId).ToList());

        public Task InsertAsync(DomainException e, CancellationToken ct)
        {
            Items.Add(e);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DomainException e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeLinkRepo : IAccountContactLinkRepository
    {
        public List<AccountContactLink> Items { get; } = new();
        public int UpdateCalls { get; private set; }

        public Task<AccountContactLink?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(l => l.TenantId == t && l.Id == id && !l.IsDeleted));

        public Task<bool> ExistsActiveAsync(Guid t, Guid a, Guid c, string r, Guid? ex, CancellationToken ct) => Task.FromResult(false);

        public Task<bool> ExistsPrimaryAsync(Guid t, Guid a, string r, Guid? ex, CancellationToken ct) => Task.FromResult(false);

        public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted && l.AccountId == a).ToList());

        public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid t, Guid c, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted && l.ContactId == c).ToList());

        public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted).ToList());

        public Task InsertAsync(AccountContactLink l, CancellationToken ct)
        {
            Items.Add(l);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AccountContactLink l, CancellationToken ct)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeContactRepo : IContactRepository
    {
        public List<Contact> Items { get; } = new();
        public int UpdateCalls { get; private set; }

        public Task<Contact?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));

    public Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Contact>>(Array.Empty<Contact>());

        public Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken ct)
            => Task.FromResult(((IReadOnlyList<Contact>)Items.Where(c => c.TenantId == t && !c.IsDeleted).ToList(), (long)Items.Count, (long)Items.Count));

        public Task<IReadOnlyList<Contact>> ListAllAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Contact>)Items.Where(c => c.TenantId == t && !c.IsDeleted).ToList());

        public Task InsertAsync(Contact c, CancellationToken ct)
        {
            Items.Add(c);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Contact c, CancellationToken ct)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public List<Account> Items { get; } = new();
        public int UpdateCalls { get; private set; }

        public Task<Account?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == t && a.Id == id && !a.IsDeleted));

        public Task<Account?> GetByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == t && a.AccountCode == code && !a.IsDeleted));

        public Task<bool> ExistsByCodeAsync(Guid t, string code, Guid? ex, CancellationToken ct) => Task.FromResult(false);

        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
            => Task.FromResult(((IReadOnlyList<Account>)Items.Where(a => a.TenantId == t).ToList(), (long)Items.Count, (long)Items.Count));

        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid t, Guid parentId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Account>)new List<Account>());

        public Task<bool> WouldCreateCycleAsync(Guid t, Guid a, Guid c, CancellationToken ct) => Task.FromResult(false);

        public Task InsertAsync(Account a, CancellationToken ct)
        {
            Items.Add(a);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Account a, CancellationToken ct)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Per-set reference status so a single unpublished set can be simulated (fail-closed behaviour).</summary>
    private sealed class FakeValidator : IReferenceDataValidator
    {
        public Dictionary<string, ReferenceValidationStatus> Status { get; } = new();

        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(new ReferenceValidationResult(
                Status.TryGetValue(setCode, out var status) ? status : ReferenceValidationStatus.Valid, setCode, value));
    }

    private sealed class NoopAudit : IContactAuditPublisher
    {
        public Task PublishAsync(string e, Guid t, Guid c, string? d, CancellationToken ct) => Task.CompletedTask;
    }
}

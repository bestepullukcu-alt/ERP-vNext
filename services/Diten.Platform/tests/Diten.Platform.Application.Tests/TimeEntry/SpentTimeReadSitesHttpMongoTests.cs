using System.Net;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1b D7 / T-14 / T-15 — every MOD-0024 read site of spent time reads <see cref="ITaskSpentTimeSource"/>:
/// the task detail and its remaining estimate (on the wire), the work report's effort figures and its export (the real
/// repository over the real store). The task carries a DECOY <c>SpentHours</c> of 7 h — the field nothing writes — so a
/// site that still reads the entity shows 7 instead of the approved 2.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class SpentTimeReadSitesHttpMongoTests : TimerScenario
{
    private readonly Guid _task = Guid.NewGuid();

    public SpentTimeReadSitesHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await SeedTaskAsync(Tenant, _task, spentHoursDecoy: 7m);
        await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
            t => t.Id == _task, Builders<TaskItem>.Update.Set(t => t.EstimateHours, 10m));
    }

    /// <summary>Approves 120 minutes on the task (week W41), then submits (not decides) 60 more on the week before.</summary>
    private async Task ApprovedTwoHoursAndOneSubmittedAsync()
    {
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, _task));
        Ok(await DecideAsync(Manager, weekId, approve: true));
        Ok(await GetWeekAsync());

        const string previous = "2026-W40";
        Ok(await SaveFreshAsync(previous, Row(new DateOnly(2026, 9, 29), 60, _task)));
        Ok(await SubmitAsync(previous));
    }

    [Fact]
    public async Task The_source_returns_approved_and_submitted_minutes_separately()
    {
        await ApprovedTwoHoursAndOneSubmittedAsync();

        var spent = await InTenantAsync(sp => sp.GetRequiredService<ITaskSpentTimeSource>().SpentTimeAsync([_task]));

        Assert.Equal(new TaskSpentTime(120, 60), spent[_task]);
    }

    [Fact]
    public async Task The_task_detail_and_its_remaining_estimate_read_the_approved_total()
    {
        await ApprovedTwoHoursAndOneSubmittedAsync();

        var detail = await Host.GetAsync($"/api/v1/tasks/{_task}", TaskToken());
        Assert.Equal(HttpStatusCode.OK, detail.Status);

        Assert.Equal(2m, detail.Data.GetProperty("spentHours").GetDecimal());       // not the decoy 7, not 3 (submitted)
        Assert.Equal(8m, detail.Data.GetProperty("remainingHours").GetDecimal());   // 10 − 2
    }

    [Fact]
    public async Task The_work_report_effort_and_its_export_read_the_approved_total()
    {
        await ApprovedTwoHoursAndOneSubmittedAsync();

        var (report, export) = await InTenantAsync<(WorkReportDto, WorkReportExportSet)>(async sp =>
        {
            var repository = new WorkReportRepository(
                Fixture.DbContext,
                sp.GetRequiredService<ITenantContext>(),
                sp.GetRequiredService<IOrganizationUnitRepository>(),
                sp.GetRequiredService<ITaskTypeRepository>(),
                new NoLegalEntities(),
                NullLogger<WorkReportRepository>.Instance,
                sp.GetRequiredService<ITaskSpentTimeSource>());
            var criteria = new WorkReportCriteria(Wednesday.AddDays(-60), Wednesday.AddDays(60), WorkReportScope.TenantWideScope());
            return (await repository.AggregateAsync(criteria), await repository.ExportAsync(criteria, 1000));
        });

        Assert.Equal(2m, report.Totals.Effort.SpentHours);
        Assert.Equal(2m, Assert.Single(export.Rows, r => r.Id == _task).SpentHours);
    }

    private async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> read)
    {
        using var scope = Host.Services.CreateScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
        {
            return await read(scope.ServiceProvider);
        }
    }

    private sealed class NoLegalEntities : ILegalEntityReferenceValidator
    {
        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default)
            => throw new NotSupportedException("No company filter is sent in these tests.");
    }
}

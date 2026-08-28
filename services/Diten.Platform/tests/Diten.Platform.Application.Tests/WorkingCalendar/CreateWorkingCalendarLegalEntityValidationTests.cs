using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Application.Features.WorkingCalendar.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.WorkingCalendar.Services;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Tests.WorkingCalendar;

public sealed class CreateWorkingCalendarLegalEntityValidationTests
{
    [Fact]
    public async Task Ac_fk_mdm_unavailable_returns_503_and_never_persists()
    {
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var repository = new Mock<IWorkingCalendarRepository>(MockBehavior.Strict);
        repository.SetupGet(x => x.CurrentTenantId).Returns(tenantId);

        var lookups = new Mock<IPlatformLookupProvider>(MockBehavior.Strict);
        lookups.Setup(x => x.GetLookupOptionsAsync(PlatformLookupKeys.Countries, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new LookupOptionDto("TR", "Türkiye", "TR") });

        var validator = new Mock<IWorkingCalendarLegalEntityValidator>(MockBehavior.Strict);
        validator.Setup(x => x.ValidateAsync(legalEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkingCalendarLegalEntityValidationResult.Unavailable);

        var currentUser = new Mock<ICurrentUserContext>(MockBehavior.Strict);
        var handler = new CreateWorkingCalendarHandler(
            repository.Object,
            lookups.Object,
            Mock.Of<IOrganizationUnitRepository>(),
            validator.Object,
            currentUser.Object);

        var result = await handler.Handle(new CreateWorkingCalendarCommand(
            "TR-LEGAL-2026", "Legal entity calendar", null, "TR", 2026,
            WorkingCalendarScopeType.LegalEntity, null, legalEntityId, null,
            WorkingCalendarStatus.Draft, WorkingCalendarSource.Manual, null, IsPlatformActor: false),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("legal_entity_validation_unavailable", result.ReasonCode);
        repository.Verify(x => x.CreateAsync(It.IsAny<Wc>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.ExistsByCodeAsync(
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()), Times.Never);
    }
}

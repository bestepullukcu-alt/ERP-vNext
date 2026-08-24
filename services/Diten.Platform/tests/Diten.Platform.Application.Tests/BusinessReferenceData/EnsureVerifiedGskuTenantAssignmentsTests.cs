using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class EnsureVerifiedGskuTenantAssignmentsTests
{
    [Fact]
    public async Task InvalidProviderConfiguration_ReturnsStable503WithoutReadsOrWrites()
    {
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetRequiredReferenceTenantId())
            .Throws(new InvalidOperationException("REFERENCE_PROVIDER_CONFIGURATION_INVALID"));
        var sut = new EnsureVerifiedGskuTenantAssignmentsHandler(repository.Object, new TenantContext());

        var response = await sut.Handle(CreateCommand(), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal("REFERENCE_PROVIDER_CONFIGURATION_INVALID", response.ReasonCode);
        repository.VerifyAll();
    }

    [Fact]
    public async Task RevokedOrDeletedHistory_Returns409BeforeAnyCreate()
    {
        var referenceTenantId = Guid.NewGuid();
        var command = CreateCommand();
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(value => value.GetTenantAssignmentForReconciliationAsync(
                command.ConsumerTenantId,
                "pack-applicability",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessReferenceDataTenantAssignment
            {
                TenantId = referenceTenantId,
                ConsumerTenantId = command.ConsumerTenantId,
                SetCode = "pack-applicability",
                AssignmentStatus = BusinessReferenceDataTenantAssignmentStatus.REVOKED
            });
        var sut = new EnsureVerifiedGskuTenantAssignmentsHandler(repository.Object, new TenantContext());

        var response = await sut.Handle(command, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal("REFERENCE_ASSIGNMENT_CONFLICT", response.ReasonCode);
        repository.Verify(value => value.EnsureActiveTenantAssignmentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingAssignments_AreCreatedForExactTwoSets_AndTenantScopeIsRestored()
    {
        var referenceTenantId = Guid.NewGuid();
        var priorTenantId = Guid.NewGuid();
        var command = CreateCommand();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(priorTenantId);
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(value => value.GetTenantAssignmentForReconciliationAsync(
                command.ConsumerTenantId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessReferenceDataTenantAssignment?)null);
        repository.Setup(value => value.EnsureActiveTenantAssignmentAsync(
                command.ConsumerTenantId,
                It.IsAny<string>(),
                command.ActorId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string setCode, string _, CancellationToken _) =>
                new BusinessReferenceDataTenantAssignmentReconciliationResult(
                    BusinessReferenceDataTenantAssignmentReconciliationOutcome.Created,
                    new BusinessReferenceDataTenantAssignment
                    {
                        TenantId = referenceTenantId,
                        ConsumerTenantId = command.ConsumerTenantId,
                        SetCode = setCode
                    }));
        var sut = new EnsureVerifiedGskuTenantAssignmentsHandler(repository.Object, tenantContext);

        var response = await sut.Handle(command, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal(priorTenantId, tenantContext.TenantId);
        repository.Verify(value => value.EnsureActiveTenantAssignmentAsync(
            command.ConsumerTenantId, "pack-applicability", command.ActorId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(value => value.EnsureActiveTenantAssignmentAsync(
            command.ConsumerTenantId, "uom", command.ActorId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(value => value.EnsureActiveTenantAssignmentAsync(
            command.ConsumerTenantId, It.IsNotIn("pack-applicability", "uom"), command.ActorId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExistingActiveAssignments_AreIdempotentWithoutWrites()
    {
        var referenceTenantId = Guid.NewGuid();
        var command = CreateCommand();
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository.Setup(value => value.GetRequiredReferenceTenantId()).Returns(referenceTenantId);
        repository.Setup(value => value.GetTenantAssignmentForReconciliationAsync(
                command.ConsumerTenantId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string setCode, CancellationToken _) => new BusinessReferenceDataTenantAssignment
            {
                TenantId = referenceTenantId,
                ConsumerTenantId = command.ConsumerTenantId,
                SetCode = setCode
            });
        var sut = new EnsureVerifiedGskuTenantAssignmentsHandler(repository.Object, new TenantContext());

        var response = await sut.Handle(command, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        repository.Verify(value => value.EnsureActiveTenantAssignmentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static EnsureVerifiedGskuTenantAssignmentsCommand CreateCommand() =>
        new(Guid.NewGuid(), "pilot-actor", "pilot-run");
}

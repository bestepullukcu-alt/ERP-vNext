namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// What every product-identity workflow operation records about the service faults it is waiting through, so that
/// one set of rules (Application: ProductIdentityWorkflowFaultRules) can read it for every kind of operation.
/// </summary>
public interface IProductIdentityWorkflowFaultFields
{
    /// <summary>The worker's consecutive failed attempts in the current streak; a user's attempts never count.</summary>
    int ServiceFaultCount { get; }

    /// <summary>The first fault of the current streak of consecutive faults; null when there is none.</summary>
    long? ServiceFaultSinceUtcTicksV1 { get; }

    long UpdatedAtUtcTicksV1 { get; }
    long? NextAttemptAtUtcTicksV1 { get; }
}

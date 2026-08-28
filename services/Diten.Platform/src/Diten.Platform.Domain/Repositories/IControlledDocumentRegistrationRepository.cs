using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

public interface IControlledDocumentRegistrationRepository
{
    Task<ControlledDocumentRegistrationOperation> AddAsync(ControlledDocumentRegistrationOperation operation, CancellationToken ct = default);
    Task<ControlledDocumentRegistrationOperation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ControlledDocumentRegistrationOperation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<ControlledDocumentRegistrationOperation?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default);
    Task<ControlledDocumentRegistrationOperation?> GetByMasterRegisterEntryIdAsync(Guid masterRegisterEntryId, CancellationToken ct = default);
    Task<IReadOnlyList<ControlledDocumentRegistrationOperation>> ListByStatusAsync(ControlledDocumentRegistrationStatus status, CancellationToken ct = default);
    Task<bool> UpdateAsync(ControlledDocumentRegistrationOperation operation, CancellationToken ct = default);
}

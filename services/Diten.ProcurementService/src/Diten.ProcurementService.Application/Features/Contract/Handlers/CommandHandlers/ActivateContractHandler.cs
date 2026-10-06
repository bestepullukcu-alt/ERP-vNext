using Diten.ProcurementService.Application.Features.Contract.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using ContractStatusEnum = Diten.ProcurementService.Domain.Entities.ContractStatus;

namespace Diten.ProcurementService.Application.Features.Contract.Handlers.CommandHandlers;

/// <summary>
/// activateContract. Draft/InReview → Active geçişi; approval trail (MOD-0023) — workflowInstanceId set edilir
/// (ASSUMPTION-0144-05). Idempotent (Idempotency-Key + zaten Active → mevcut sözleşme döner, ikinci geçiş YOK). Zaten
/// Active / Expired / Terminated (Draft veya InReview değil) → 409 INVALID_STATE (sessiz overwrite YOK). Bilinmeyen
/// sözleşme / cross-tenant-LE → 404 NOT_FOUND. Eşzamanlı stale → 409.
/// </summary>
public sealed class ActivateContractHandler : IRequestHandler<ActivateContractCommand, Response<ContractDto>>
{
    private readonly IContractingRepository _repository;

    public ActivateContractHandler(IContractingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ContractDto>> Handle(ActivateContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await _repository.GetByContractIdAsync(request.ContractId, cancellationToken);
        if (contract is null)
        {
            return Response<ContractDto>.Fail("NOT_FOUND", 404);
        }

        // ── Idempotent activate: aynı key + zaten Active → mevcut sözleşme döner (ikinci geçiş YOK) ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && contract.Status == ContractStatusEnum.Active
            && string.Equals(contract.ActivateIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Response<ContractDto>.Success(ContractMapping.ToDto(contract));
        }

        // ── State gate: activate yalnız Draft/InReview'den (aksi 409 INVALID_STATE) ──
        if (contract.Status is not (ContractStatusEnum.Draft or ContractStatusEnum.InReview))
        {
            return Response<ContractDto>.Fail("INVALID_STATE", 409);
        }

        var expectedVersion = contract.Version;

        // ── Approval trail (MOD-0023 seam): activate approval instance başlatır/ilişkilendirir (ASSUMPTION-0144-05) ──
        contract.Status = ContractStatusEnum.Active;
        contract.WorkflowInstanceId = GenerateWorkflowInstanceId();
        contract.ActivateIdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        var ok = await _repository.UpdateAsync(contract, expectedVersion, cancellationToken);
        if (!ok)
        {
            // Eşzamanlı değişim (stale) → geçiş uygulanamadı.
            return Response<ContractDto>.Fail("INVALID_STATE", 409);
        }

        return Response<ContractDto>.Success(ContractMapping.ToDto(contract));
    }

    private static string GenerateWorkflowInstanceId()
        => "WF-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}

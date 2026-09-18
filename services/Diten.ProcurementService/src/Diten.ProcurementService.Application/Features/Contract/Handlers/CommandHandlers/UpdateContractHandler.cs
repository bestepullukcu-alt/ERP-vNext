using Diten.ProcurementService.Application.Features.Contract.Commands;
using Diten.ProcurementService.Application.Features.Contract.Validators;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using ClauseRefEntity = Diten.ProcurementService.Domain.Entities.ClauseRef;
using ContractStatusEnum = Diten.ProcurementService.Domain.Entities.ContractStatus;

namespace Diten.ProcurementService.Application.Features.Contract.Handlers.CommandHandlers;

/// <summary>
/// updateContract (ASSUMPTION-0144-01 additive). Yalnız Draft güncellenebilir (aksi 409 INVALID_STATE). SupplierId
/// IMMUTABLE. rfxId verilirse MOD-0145 CONSUME (fail-closed → 404). clauseId'ler clause library'de var olmalı (yoksa
/// 422). currency boşsa server default (ASSUMPTION-0144-04). evidenceRef yalnız referans (binary YOK). Optimistic
/// concurrency; cross-tenant/LE → 404.
/// </summary>
public sealed class UpdateContractHandler : IRequestHandler<UpdateContractCommand, Response<ContractDto>>
{
    private readonly IContractingRepository _repository;
    private readonly IRfxRepository _rfxRepository;

    public UpdateContractHandler(IContractingRepository repository, IRfxRepository rfxRepository)
    {
        _repository = repository;
        _rfxRepository = rfxRepository;
    }

    public async Task<Response<ContractDto>> Handle(UpdateContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await _repository.GetByContractIdAsync(request.ContractId, cancellationToken);
        if (contract is null)
        {
            return Response<ContractDto>.Fail("NOT_FOUND", 404);
        }

        // ── State gate: yalnız Draft güncellenebilir (aktive/onaya gönderilmiş sözleşme dondurulur) ──
        if (contract.Status != ContractStatusEnum.Draft)
        {
            return Response<ContractDto>.Fail("INVALID_STATE", 409);
        }

        // ── İş kuralı (422): zorunlu alanlar + tarih formatı/aralığı ──
        if (string.IsNullOrWhiteSpace(request.Title)
            || request.Title.Trim().Length > 200
            || !ContractValidationRules.IsValidDate(request.EffectiveFrom)
            || (!string.IsNullOrWhiteSpace(request.EffectiveTo) && !ContractValidationRules.IsValidDate(request.EffectiveTo))
            || !ContractValidationRules.IsEffectiveRangeValid(request.EffectiveFrom, request.EffectiveTo))
        {
            return Response<ContractDto>.Fail("VALIDATION_FAILED", 422);
        }

        // ── SOURCING award/rfx (0145) consume — verilirse fail-closed → 404 UNKNOWN_REFERENCE ──
        string? rfxId = null;
        if (!string.IsNullOrWhiteSpace(request.RfxId))
        {
            rfxId = request.RfxId!.Trim();
            var rfx = await _rfxRepository.GetByRfxIdAsync(rfxId, cancellationToken);
            if (rfx is null)
            {
                return Response<ContractDto>.Fail("UNKNOWN_REFERENCE", 404);
            }
        }

        // ── Clause referansları clause library'de var olmalı (yoksa 422) ──
        var clauseInputs = request.Clauses ?? new List<ClauseRefInput>();
        var clauses = new List<ClauseRefEntity>();
        foreach (var c in clauseInputs)
        {
            if (string.IsNullOrWhiteSpace(c.ClauseId))
            {
                return Response<ContractDto>.Fail("VALIDATION_FAILED", 422);
            }
            var clauseId = c.ClauseId.Trim();
            if (!await _repository.ExistsClauseAsync(clauseId, cancellationToken))
            {
                return Response<ContractDto>.Fail("VALIDATION_FAILED", 422);
            }
            clauses.Add(new ClauseRefEntity
            {
                ClauseId = clauseId,
                Deviation = c.Deviation,
                DeviationText = string.IsNullOrWhiteSpace(c.DeviationText) ? null : c.DeviationText!.Trim()
            });
        }

        var expectedVersion = contract.Version;

        contract.RfxId = rfxId;
        contract.Title = request.Title.Trim();
        contract.EffectiveFrom = request.EffectiveFrom.Trim();
        contract.EffectiveTo = string.IsNullOrWhiteSpace(request.EffectiveTo) ? null : request.EffectiveTo!.Trim();
        contract.Currency = string.IsNullOrWhiteSpace(request.Currency)
            ? ContractingContract.DefaultCurrency
            : request.Currency!.Trim();
        contract.Clauses = clauses;
        contract.EvidenceRefs = (request.EvidenceRefs ?? new List<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToList();

        var ok = await _repository.UpdateAsync(contract, expectedVersion, cancellationToken);
        if (!ok)
        {
            return Response<ContractDto>.Fail("INVALID_STATE", 409);
        }

        return Response<ContractDto>.Success(ContractMapping.ToDto(contract));
    }
}

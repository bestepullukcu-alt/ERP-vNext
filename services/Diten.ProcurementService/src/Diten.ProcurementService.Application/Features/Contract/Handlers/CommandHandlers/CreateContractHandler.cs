using Diten.ProcurementService.Application.Features.Contract.Commands;
using Diten.ProcurementService.Application.Features.Contract.Validators;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using ClauseRefEntity = Diten.ProcurementService.Domain.Entities.ClauseRef;
using ContractEntity = Diten.ProcurementService.Domain.Entities.Contract;
using ContractStatusEnum = Diten.ProcurementService.Domain.Entities.ContractStatus;

namespace Diten.ProcurementService.Application.Features.Contract.Handlers.CommandHandlers;

/// <summary>
/// createContract. Idempotent (Idempotency-Key → replay mevcut sözleşmeyi döner, yeni yaratmaz). CONSUME-don't-own
/// (fail-closed): supplierId MOD-0140 (bilinmeyen → 404 UNKNOWN_REFERENCE), rfxId verilirse MOD-0145 award (bilinmeyen
/// → 404). effectiveFrom zorunlu + geçerli; effectiveTo ≥ effectiveFrom (aksi 422). currency boşsa server LE base
/// default (ASSUMPTION-0144-04). clauses[].clauseId clause library'de var olmalı (yoksa 422 VALIDATION_FAILED).
/// evidenceRefs yalnız REFERANS (MOD-0029/0031); binary saklanmaz. Sözleşme Draft doğar.
/// </summary>
public sealed class CreateContractHandler : IRequestHandler<CreateContractCommand, Response<ContractDto>>
{
    private readonly IContractingRepository _repository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IRfxRepository _rfxRepository;

    public CreateContractHandler(
        IContractingRepository repository,
        ISupplierRepository supplierRepository,
        IRfxRepository rfxRepository)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
        _rfxRepository = rfxRepository;
    }

    public async Task<Response<ContractDto>> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotent create: aynı Idempotency-Key ile replay → mevcut sözleşme döner, yeni yaratmaz ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await _repository.GetByIdempotencyKeyAsync(request.IdempotencyKey!, cancellationToken);
            if (replay is not null)
            {
                return Response<ContractDto>.Success(ContractMapping.ToDto(replay), 201);
            }
        }

        // ── İş kuralı (contract Unprocessable 422): zorunlu alanlar + tarih formatı/aralığı ──
        if (string.IsNullOrWhiteSpace(request.SupplierId)
            || string.IsNullOrWhiteSpace(request.Title)
            || request.Title.Trim().Length > 200
            || !ContractValidationRules.IsValidDate(request.EffectiveFrom)
            || (!string.IsNullOrWhiteSpace(request.EffectiveTo) && !ContractValidationRules.IsValidDate(request.EffectiveTo))
            || !ContractValidationRules.IsEffectiveRangeValid(request.EffectiveFrom, request.EffectiveTo))
        {
            return Response<ContractDto>.Fail("VALIDATION_FAILED", 422);
        }

        var supplierId = request.SupplierId.Trim();

        // ── SUPPLIER (0140) consume — fail-closed: bilinmeyen supplier → 404 UNKNOWN_REFERENCE ──
        var supplier = await _supplierRepository.GetBySupplierIdAsync(supplierId, cancellationToken);
        if (supplier is null)
        {
            return Response<ContractDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        // ── SOURCING award/rfx (0145) consume — verilirse fail-closed: bilinmeyen rfx → 404 UNKNOWN_REFERENCE ──
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

        // ── Clause referansları clause library'de var olmalı (yoksa 422 VALIDATION_FAILED) ──
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

        // ── currency boşsa server LE base default (ASSUMPTION-0144-04) ──
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? ContractingContract.DefaultCurrency
            : request.Currency!.Trim();

        var evidenceRefs = (request.EvidenceRefs ?? new List<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToList();

        var entity = new ContractEntity
        {
            ContractId = GenerateContractId(),
            SupplierId = supplierId,
            RfxId = rfxId,
            Title = request.Title.Trim(),
            Status = ContractStatusEnum.Draft,
            EffectiveFrom = request.EffectiveFrom.Trim(),
            EffectiveTo = string.IsNullOrWhiteSpace(request.EffectiveTo) ? null : request.EffectiveTo!.Trim(),
            Currency = currency,
            Clauses = clauses,
            EvidenceRefs = evidenceRefs,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<ContractDto>.Success(ContractMapping.ToDto(created), 201);
    }

    private static string GenerateContractId()
        => "CTR-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}

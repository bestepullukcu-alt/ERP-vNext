using Diten.ProcurementService.Application.Features.Clause.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using ClauseEntity = Diten.ProcurementService.Domain.Entities.Clause;

namespace Diten.ProcurementService.Application.Features.Clause.Handlers.CommandHandlers;

/// <summary>
/// createClause. Idempotent (Idempotency-Key → replay mevcut clause'ı döner, yeni yaratmaz). category/title/body
/// zorunlu (aksi 422 VALIDATION_FAILED). (Category+Title) tenant+LE bazında duplicate → 409 DUPLICATE_CLAUSE (kayıt
/// yazılmaz). Clause IMMUTABLE (ASSUMPTION-0144-03).
/// </summary>
public sealed class CreateClauseHandler : IRequestHandler<CreateClauseCommand, Response<ClauseDto>>
{
    private readonly IContractingRepository _repository;

    public CreateClauseHandler(IContractingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ClauseDto>> Handle(CreateClauseCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotent create: aynı Idempotency-Key ile replay → mevcut clause döner, yeni yaratmaz ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await _repository.GetClauseByIdempotencyKeyAsync(request.IdempotencyKey!, cancellationToken);
            if (replay is not null)
            {
                return Response<ClauseDto>.Success(ClauseMapping.ToDto(replay), 201);
            }
        }

        // ── İş kuralı (contract Unprocessable 422): zorunlu alanlar ──
        if (string.IsNullOrWhiteSpace(request.Category)
            || string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Body))
        {
            return Response<ClauseDto>.Fail("VALIDATION_FAILED", 422);
        }

        var category = request.Category.Trim();
        var title = request.Title.Trim();

        // ── Duplicate (Category+Title) → 409 DUPLICATE_CLAUSE (kayıt yazılmaz) ──
        if (await _repository.ExistsClauseByCategoryTitleAsync(category, title, cancellationToken))
        {
            return Response<ClauseDto>.Fail("DUPLICATE_CLAUSE", 409);
        }

        var entity = new ClauseEntity
        {
            ClauseId = GenerateClauseId(),
            Category = category,
            Title = title,
            Body = request.Body.Trim(),
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };

        var created = await _repository.CreateClauseAsync(entity, cancellationToken);
        return Response<ClauseDto>.Success(ClauseMapping.ToDto(created), 201);
    }

    private static string GenerateClauseId()
        => "CL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}

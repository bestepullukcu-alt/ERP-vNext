using MediatR;

namespace Diten.MdmService.Application.Features.Sample.Commands;

/// <summary>
/// POST /api/samples — yeni SampleEntity oluşturur.
/// TenantId request body'de YOKTUR; server-side TenantContext'ten set edilir.
/// </summary>
public sealed record CreateSampleCommand(
    string Name,
    string? Description
) : IRequest<CreateSampleResult>;

public sealed record CreateSampleResult(
    Guid Id,
    string Name,
    string? Description,
    Guid TenantId,
    DateTimeOffset CreatedAt
);

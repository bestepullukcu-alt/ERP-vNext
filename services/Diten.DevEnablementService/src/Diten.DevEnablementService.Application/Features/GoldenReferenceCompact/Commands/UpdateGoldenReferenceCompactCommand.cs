using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;

public sealed class UpdateGoldenReferenceCompactCommand : IRequest<Response<bool>>
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public string? Category { get; set; }
    public string? GroupKey { get; set; }
    public string? SourceSystem { get; set; }
    public string? Owner { get; set; }
    public string? Version { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

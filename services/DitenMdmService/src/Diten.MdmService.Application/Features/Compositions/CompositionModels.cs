using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.Compositions;

public abstract class CompositionUpsertRequestBase
{
    public string FormulationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid DosageFormId { get; set; }
    public decimal StrengthValue { get; set; }
    public Guid StrengthUnitId { get; set; }
    public decimal TechnicalFillAmount { get; set; }
    public Guid? TechnicalFillUnitId { get; set; }
    public List<CompositionComponentDto> Components { get; set; } = [];
}

public class CompositionComponentDto
{
    public int Sequence { get; set; }
    public Guid ComponentId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public CompositionComponentType ComponentType { get; set; }
    public decimal Quantity { get; set; }
    public Guid UnitId { get; set; }
}

public class CompositionListItemDto
{
    public Guid Id { get; set; }
    public string FormulationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public string DosageForm { get; set; } = string.Empty;
    public decimal StrengthValue { get; set; }
    public string StrengthUnit { get; set; } = string.Empty;
    public string Strength => $"{StrengthValue} {StrengthUnit}";
    public string LifecycleState { get; set; } = string.Empty;
    public CompositionLifecycleState LifecycleStateEnum { get; set; }
}

public sealed class CompositionDetailDto
{
    public Guid Id { get; set; }
    public string FormulationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CompositionLifecycleState LifecycleState { get; set; }
    public Guid? CurrentVersionId { get; set; }
    
    public CompositionVersionDto? CurrentVersion { get; set; }
    public List<CompositionVersionSummaryDto> VersionHistory { get; set; } = [];
}

public class CompositionVersionDto
{
    public Guid Id { get; set; }
    public int VersionNo { get; set; }
    public string VersionLabel => $"v{VersionNo}";
    public CompositionVersionStatus Status { get; set; }
    public bool IsCurrent { get; set; }
    public Guid DosageFormId { get; set; }
    public string DosageFormName { get; set; } = string.Empty;
    public decimal StrengthValue { get; set; }
    public Guid StrengthUnitId { get; set; }
    public string StrengthUnitName { get; set; } = string.Empty;
    public decimal TechnicalFillAmount { get; set; }
    public Guid? TechnicalFillUnitId { get; set; }
    public string TechnicalFillUnitName { get; set; } = string.Empty;
    public List<CompositionComponentDto> Components { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public class CompositionVersionSummaryDto
{
    public Guid Id { get; set; }
    public int VersionNo { get; set; }
    public string VersionLabel => $"v{VersionNo}";
    public CompositionVersionStatus Status { get; set; }
    public bool IsCurrent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal static class CompositionMapping
{
    public static CompositionListItemDto ToListDto(
        Composition entity,
        CompositionVersion? currentVersion,
        string dosageFormName,
        string strengthUnitName)
    {
        return new CompositionListItemDto
        {
            Id = entity.Id,
            FormulationCode = entity.FormulationCode,
            Name = entity.Name,
            VersionLabel = currentVersion?.DisplayName ?? "-",
            DosageForm = dosageFormName,
            StrengthValue = currentVersion?.StrengthValue ?? 0,
            StrengthUnit = strengthUnitName,
            LifecycleState = entity.LifecycleState.ToString(),
            LifecycleStateEnum = entity.LifecycleState
        };
    }

    public static CompositionVersionDto ToVersionDto(
        CompositionVersion version,
        string dosageFormName,
        string strengthUnitName,
        string fillUnitName)
    {
        return new CompositionVersionDto
        {
            Id = version.Id,
            VersionNo = version.VersionNo,
            Status = version.Status,
            IsCurrent = version.IsCurrent,
            DosageFormId = version.DosageFormId,
            DosageFormName = dosageFormName,
            StrengthValue = version.StrengthValue,
            StrengthUnitId = version.StrengthUnitId,
            StrengthUnitName = strengthUnitName,
            TechnicalFillAmount = version.TechnicalFillAmount,
            TechnicalFillUnitId = version.TechnicalFillUnitId,
            TechnicalFillUnitName = fillUnitName,
            CreatedAt = version.CreatedAt,
            Components = version.Components.Select(c => new CompositionComponentDto
            {
                Sequence = c.Sequence,
                ComponentId = c.ComponentId,
                ComponentName = c.ComponentName,
                ComponentType = c.ComponentType,
                Quantity = c.Quantity,
                UnitId = c.UnitId
            }).OrderBy(c => c.Sequence).ToList()
        };
    }
}

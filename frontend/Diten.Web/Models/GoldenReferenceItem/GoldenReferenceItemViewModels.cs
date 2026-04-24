using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.GoldenReferenceItem;

public sealed class GoldenReferenceItemEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class GoldenReferenceItemDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

public sealed class GoldenReferenceItemSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}

namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// Bootstrap doğrulaması için örnek entity.
/// Gerçek domain entity'leri bu namespace'e eklenecek.
/// </summary>
public class SampleEntity : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

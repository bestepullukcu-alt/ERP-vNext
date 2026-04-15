namespace Diten.Web.Models;

public sealed class ApiListResponse<T>
{
    public List<T> Data { get; set; } = [];
}

public class LookupApiViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

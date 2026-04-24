namespace Diten.AuthService.Application.Common.Models;

public sealed record PaginatedResult<T>(
    IEnumerable<T> Items,
    long TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

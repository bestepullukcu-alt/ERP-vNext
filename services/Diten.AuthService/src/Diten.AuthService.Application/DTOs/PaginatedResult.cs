namespace Diten.AuthService.Application.DTOs;

public sealed record PaginatedResult<T>(
    IEnumerable<T> Items,
    long TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}

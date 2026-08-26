using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public sealed record GlobalProductCreateResult(
    bool Succeeded,
    GlobalProduct? GlobalProduct,
    string? ErrorCode = null);

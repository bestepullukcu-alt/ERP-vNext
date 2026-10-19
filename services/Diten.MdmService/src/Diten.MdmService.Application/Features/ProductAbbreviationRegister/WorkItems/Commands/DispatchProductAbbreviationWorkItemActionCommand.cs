using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands;

public sealed record DispatchProductAbbreviationWorkItemActionCommand(
    Guid ItemId,
    string ActionCode,
    string? ProviderCode,
    int? ExpectedVersion,
    string? Reason,
    string? Note,
    bool HasUnmappedFields)
    : IRequest<ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>>;

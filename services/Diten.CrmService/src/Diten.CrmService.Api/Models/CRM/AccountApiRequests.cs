namespace Diten.CrmService.Api.Models.CRM;

public sealed record LinkParentRequest(Guid ParentAccountId);

public sealed record UpsertAttributeRequest(string AttributeCode, string? Value);

public sealed record BulkDeleteAccountsRequest(IReadOnlyList<Guid> Ids);

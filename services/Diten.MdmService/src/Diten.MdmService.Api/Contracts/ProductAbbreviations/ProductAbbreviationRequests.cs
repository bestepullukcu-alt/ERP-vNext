namespace Diten.MdmService.Api.Contracts.ProductAbbreviations;

public sealed record RequestAllocationRequest(Guid GlobalProductId, string Abbreviation);

public sealed record CancelAllocationRequest(int ExpectedVersion, string? Reason = null);

public sealed record ApproveAllocationRequest(
    int ExpectedVersion,
    int? ExpectedFormerVersion = null,
    string? Reason = null);

public sealed record RejectAllocationRequest(int ExpectedVersion, string Reason);

public sealed record InitiateCorrectionRequest(
    int ExpectedVersion,
    string ReplacementAbbreviation,
    string Reason);

public sealed record RequestRetirementRequest(int ExpectedVersion, string Reason);

public sealed record ApproveRetirementRequest(int ExpectedVersion, string? Reason = null);

public sealed record RejectRetirementRequest(int ExpectedVersion, string Reason);

using System.Text.Json.Serialization;
using Diten.Platform.Domain.Enums.RecordCodes;

namespace Diten.Platform.Application.Features.RecordCodes;

/// <summary>RECORD-CODE-ALLOCATE 1.0.0 §3 — the 200 <c>data</c>: the code and the counter number it carries, nothing else.</summary>
public sealed record RecordCodeAllocationResult(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("sequence")] long Sequence);

/// <summary>
/// The allocate body after the API parser (step 5): <see cref="PrefixScope"/> has the default applied (a v0 body without
/// <c>prefixScope</c> is LegalEntity) and <see cref="LegalEntityId"/> is the parsed UUID value, whatever its text form.
/// </summary>
public sealed record AllocateRecordCodeRequest(
    string RecordType,
    RecordCodePrefixScope PrefixScope,
    Guid? LegalEntityId,
    string IdempotencyKey);

/// <summary>The tenant's group-prefix setting as the platform administrator's GET returns it.</summary>
public sealed record RecordCodeGroupPrefixDto(
    Guid TenantId,
    string GroupPrefix,
    DateTime AlignedAtUtc,
    DateTime? LockedAtUtc,
    string EffectiveOrgPrefix);

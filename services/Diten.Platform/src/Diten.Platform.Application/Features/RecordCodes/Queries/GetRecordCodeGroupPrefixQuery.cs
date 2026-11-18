using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.RecordCodes.Queries;

/// <summary>MOD-0040-FU02 — the platform administrator reads one tenant's group-prefix setting (not audited, §21.2).</summary>
public sealed record GetRecordCodeGroupPrefixQuery(Guid TenantId) : IRequest<Response<RecordCodeGroupPrefixDto>>;

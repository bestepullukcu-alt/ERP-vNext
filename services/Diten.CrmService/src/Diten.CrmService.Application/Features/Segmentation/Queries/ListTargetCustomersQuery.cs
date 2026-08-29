using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Queries;

/// <summary>The hand-written membership rows of one segment. Derived members are NOT here and never will be — that is
/// what makes this list answerable as "what did a person decide?".</summary>
public sealed record ListTargetCustomersQuery(
    Guid SegmentId,
    string? MembershipMode,
    bool IncludeArchived) : IRequest<Response<TargetCustomerListDto>>;

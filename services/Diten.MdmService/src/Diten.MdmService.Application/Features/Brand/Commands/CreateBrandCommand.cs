using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Commands;

// `Actor` is the audit identity (CreatedBy/UpdatedBy/ArchivedBy). MdmService has no ICurrentUser abstraction
// and introducing one would reach outside this pack's repo scope, so the controller reads the JWT subject and
// passes it in. It is audit metadata only — it is never used for authorization, which stays on [HasPermission].
public sealed record CreateBrandCommand(BrandWriteRequest Request, string? Actor = null) : IRequest<Response<Guid>>;

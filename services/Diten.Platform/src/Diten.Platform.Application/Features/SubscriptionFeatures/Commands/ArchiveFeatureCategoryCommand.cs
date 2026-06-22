using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Commands;

public sealed record ArchiveFeatureCategoryCommand(Guid Id, byte[]? RowVersion) : IRequest<Response<NoContent>>;

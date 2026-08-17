using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ESignature.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.ESignature;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers.QueryHandlers;

public sealed class GetSignatureEnvelopeListHandler
    : IRequestHandler<GetSignatureEnvelopeListQuery, Response<SignatureEnvelopePageDto>>
{
    private readonly IESignatureRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetSignatureEnvelopeListHandler(IESignatureRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<SignatureEnvelopePageDto>> Handle(
        GetSignatureEnvelopeListQuery query,
        CancellationToken ct)
    {
        using var tenantScope = TenantScope.BeginPlatform(_tenantContext, query.TargetTenantId);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var envelopes = await _repository.GetEnvelopesAsync((page - 1) * pageSize, pageSize, ct);
        var totalCount = await _repository.CountEnvelopesAsync(ct);
        var items = new List<SignatureEnvelopeListItemDto>(envelopes.Count);
        foreach (var envelope in envelopes)
        {
            var participants = await _repository.GetParticipantsAsync(envelope.Id, ct);
            items.Add(new SignatureEnvelopeListItemDto(
                envelope.Id,
                envelope.TenantId,
                envelope.EnvelopeNumber,
                envelope.SubjectType,
                envelope.SubjectId,
                envelope.SubjectVersion,
                envelope.Status.ToString(),
                participants.Count,
                participants.Count(x => x.Status == SignatureParticipantStatus.Signed),
                envelope.CreatedAt,
                envelope.CompletedAt,
                envelope.Version));
        }

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return Response<SignatureEnvelopePageDto>.Success(
            new SignatureEnvelopePageDto(items, page, pageSize, totalCount, totalPages));
    }
}

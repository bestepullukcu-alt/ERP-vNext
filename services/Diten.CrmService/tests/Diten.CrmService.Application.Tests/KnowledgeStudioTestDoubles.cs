using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Tests;

// ---------------- shared knowledge studio test doubles ----------------
// WP-KP-4 — moved out of the retired ContentSetContextTests (the content set authoring surface is gone). The names keep
// their WP-SB-1R prefix: the knowledge path tests (KP-1 / KP-2 / KP-3) use them (WP-CLN-1 removed the set / revision
// repository doubles with the code).

/// <summary>BRD reference data for the set context: COUNTRY_CODES (TR, UZ, GB, QQ) and country-content-languages
/// (TR: tr · UZ: uz, ru · GB: en · QQ: none). <see cref="Published"/> false = unreadable.</summary>
internal sealed class ContentSetTestCatalog : IReferenceDataCatalogReader
{
    public bool Published { get; set; } = true;

    public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
    {
        if (!Published)
        {
            return Task.FromResult(ReferenceSetSnapshot.NotPublished(setCode));
        }

        IReadOnlyList<ReferenceValueSnapshot> values = setCode switch
        {
            "COUNTRY_CODES" => new[] { "TR", "UZ", "GB", "QQ" }
                .Select(c => new ReferenceValueSnapshot(c, c, null, true, false, null)).ToList(),
            "country-content-languages" => new[]
            {
                Row("TR", "tr"), Row("UZ", "uz,ru"), Row("GB", "en"),
                new ReferenceValueSnapshot("QQ", "QQ", null, true, false, null)
            },
            _ => Array.Empty<ReferenceValueSnapshot>()
        };
        return Task.FromResult(new ReferenceSetSnapshot(setCode, true, values));
    }

    private static ReferenceValueSnapshot Row(string country, string languages)
        => new(country, country, null, true, false, new Dictionary<string, string> { ["Languages"] = languages });
}

internal sealed class ContentSetTestSubjects : ISubjectRepository
{
    public List<Subject> Items { get; } = new();
    public Task<Subject?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == t && s.Id == id));
    public Task<IReadOnlyList<Subject>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<Subject>)Items.Where(s => s.TenantId == t).ToList());
    public Task<Subject?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<Subject?>(null);
    public Task InsertAsync(Subject subject, CancellationToken ct) { Items.Add(subject); return Task.CompletedTask; }
    public Task UpdateAsync(Subject subject, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestProfiles : IAudienceProfileRepository
{
    public List<AudienceProfile> Items { get; } = new();
    public Task<AudienceProfile?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id));
    public Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<AudienceProfile>)Items.Where(p => p.TenantId == t).ToList());
    public Task<AudienceProfile?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<AudienceProfile?>(null);
    public Task InsertAsync(AudienceProfile profile, CancellationToken ct) { Items.Add(profile); return Task.CompletedTask; }
    public Task UpdateAsync(AudienceProfile profile, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestTemplates : IConceptChainTemplateRepository
{
    public List<ConceptChainTemplate> Items { get; } = new();
    public Task<ConceptChainTemplate?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
    public Task<IReadOnlyList<ConceptChainTemplate>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t).ToList());
    public Task<IReadOnlyList<ConceptChainTemplate>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t && x.SubjectId == s).ToList());
    public Task<IReadOnlyList<ConceptChainTemplate>> ListByCodeAsync(Guid t, Guid s, string code, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t && x.ChainCode == code).ToList());
    public Task InsertAsync(ConceptChainTemplate entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
    public Task UpdateAsync(ConceptChainTemplate entity, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestContents : IKnowledgeContentRepository
{
    public List<KnowledgeContent> Items { get; } = new();
    public Task<KnowledgeContent?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id));
    public Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<KnowledgeContent>)Items.Where(c => c.TenantId == t).ToList());
    public Task<KnowledgeContent?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.ContentCode == code && !c.IsArchived()));
    public Task InsertAsync(KnowledgeContent content, CancellationToken ct) { Items.Add(content); return Task.CompletedTask; }
    public Task UpdateAsync(KnowledgeContent content, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ContentSetTestClaims : IClaimRepository
{
    public List<Claim> Items { get; } = new();
    public Task<Claim?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id));
    public Task<IReadOnlyList<Claim>> ListAsync(Guid t, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<Claim>)Items.Where(c => c.TenantId == t).ToList());
    public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<Claim>)Items.Where(c => c.TenantId == t && c.ClaimCode == code).ToList());
    public Task<Claim?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.ClaimCode == code && !c.IsArchived()));
    public Task InsertAsync(Claim entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
    public Task UpdateAsync(Claim entity, CancellationToken ct) => Task.CompletedTask;
}

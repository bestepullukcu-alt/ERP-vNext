using System.Net;
using System.Text.Json;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.Knowledge.Concept;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Infrastructure.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.CrmService.Application.Tests.Audit;

/// <summary>
/// CRM-BP87-01 — measures the <c>SourceModule</c> the HTTP audit forwarder actually sends to the MOD-0021 append
/// contract, per publisher seam. Content Studio (claim / composition) events carry the Blueprint 8.7 identity
/// MOD-0363 (formerly CAND-CAP-0011); a regression to the deprecated alias turns this red.
/// </summary>
public sealed class HttpCrmAuditPublisherSourceModuleTests
{
    private static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    /// <summary>Records the JSON body of every request and answers 202.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }

    private static (HttpCrmAuditPublisher Publisher, CapturingHandler Handler) Create()
    {
        var handler = new CapturingHandler();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:BaseUrl"] = "http://gateway.test" })
            .Build();
        var publisher = new HttpCrmAuditPublisher(
            new HttpClient(handler), configuration, new HttpContextAccessor(), NullLogger<HttpCrmAuditPublisher>.Instance);
        return (publisher, handler);
    }

    private static string SentSourceModule(CapturingHandler handler)
    {
        var body = Assert.Single(handler.Bodies);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("sourceModule").GetString()!;
    }

    [Fact]
    public async Task Content_composition_events_are_sent_with_source_module_MOD_0363()
    {
        var (publisher, handler) = Create();

        await ((IContentCompositionAuditPublisher)publisher).PublishAsync(
            "crm.claim.approved", Tenant, "Claim", Guid.NewGuid(), 3, "count=1", CancellationToken.None);

        Assert.Equal("MOD-0363", SentSourceModule(handler));
    }

    [Fact]
    public async Task The_other_seams_keep_their_source_modules()
    {
        var cases = new (string Expected, Func<HttpCrmAuditPublisher, Task> Publish)[]
        {
            ("MOD-0150", p => ((IContactAuditPublisher)p).PublishAsync("crm.contact.created", Tenant, Guid.NewGuid(), null, CancellationToken.None)),
            ("MOD-0150", p => ((IAccountAuditPublisher)p).PublishAsync("crm.account.created", Tenant, Guid.NewGuid(), null, CancellationToken.None)),
            ("MOD-0162", p => ((IKnowledgeConceptAuditPublisher)p).PublishAsync("crm.concept.created", Tenant, "ConceptNode", Guid.NewGuid(), 1, null, CancellationToken.None)),
            ("MOD-0155", p => ((IVisitEvidenceAuditPublisher)p).PublishAsync("crm.visit-evidence.upserted", Tenant, "VisitEvidencePolicyScope", Guid.NewGuid(), 1, null, CancellationToken.None)),
        };

        foreach (var (expected, publish) in cases)
        {
            var (publisher, handler) = Create();
            await publish(publisher);
            Assert.Equal(expected, SentSourceModule(handler));
        }
    }
}

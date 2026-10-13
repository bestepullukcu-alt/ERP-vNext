using Diten.Platform.Application.Features.ESignature.Providers;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E5W2 (O-E5W1-1) — the <c>workflow / workflow-task</c> subject shows its summary labels in the request language
/// (tr / en), and the language never reaches the canonical fields: one task has ONE subject hash in every language, so a
/// signature taken in Turkish verifies <c>valid</c> for an English reader (and the reverse).
/// </summary>
public sealed class WorkflowTaskSubjectLanguageTests
{
    private readonly Mock<IApprovalTaskRepository> _tasks = new();
    private readonly Mock<IWorkflowInstanceRepository> _instances = new();
    private readonly ApprovalTask _task;

    public WorkflowTaskSubjectLanguageTests()
    {
        var tenant = Guid.NewGuid();
        var instance = new WorkflowInstance
        {
            TenantId = tenant,
            TemplateId = Guid.NewGuid(),
            WorkflowTemplateId = Guid.NewGuid(),
            ObjectType = "inventory.quarantine-decision",
            ObjectId = "KRN-2026-0412",
            ObjectRef = "KRN-2026-0412",
            DisplayContext = new WorkflowDisplayContextSnapshot
            {
                Title = "Karantina kararı KRN-2026-0412",
                Subtitle = "Lot PRS-26A014 · 1200 EA",
                SourceModule = "MOD-0175",
                DeepLinkUrl = "/Quarantine/Details/x",
                Chips = ["Serbest bırak"]
            }
        };
        _task = new ApprovalTask
        {
            TenantId = tenant,
            WorkflowInstanceId = instance.Id,
            StageCode = "QA",
            StepCode = "QA_APPROVAL",
            SignatureRequired = true,
            SignatureMeaning = "APPROVE",
            AssigneeRef = "user:" + Guid.NewGuid().ToString("D")
        };
        _tasks.Setup(r => r.GetByIdAsync(_task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_task);
        _instances.Setup(r => r.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
    }

    private async Task<SignableSubject> ResolveAsync(string acceptLanguage)
    {
        var language = new Mock<IESignRequestLanguage>();
        language.Setup(l => l.Current()).Returns(acceptLanguage);
        var provider = new WorkflowTaskSignableSubjectProvider(_tasks.Object, _instances.Object, language.Object);
        var resolution = await provider.ResolveAsync(WorkflowTaskSignableSubject.SubjectType, _task.Id.ToString("D"), CancellationToken.None);
        Assert.Equal(SignableSubjectOutcome.Found, resolution.Outcome);
        return resolution.Subject!;
    }

    private string Hash(SignableSubject subject) =>
        ESignatureDigest.SubjectHash(WorkflowTaskSignableSubject.Module, WorkflowTaskSignableSubject.SubjectType, _task.Id.ToString("D"), subject.Fields);

    [Theory]
    [InlineData("tr", new[] { "Nesne", "Adım", "Ayrıntı", "Kaynak" })]
    [InlineData("tr-TR,tr;q=0.9,en;q=0.8", new[] { "Nesne", "Adım", "Ayrıntı", "Kaynak" })]
    [InlineData("en", new[] { "Object", "Step", "Detail", "Source" })]
    [InlineData("en-GB", new[] { "Object", "Step", "Detail", "Source" })]
    [InlineData("fr", new[] { "Object", "Step", "Detail", "Source" })] // Platform modules carry tr / en only
    [InlineData("", new[] { "Object", "Step", "Detail", "Source" })]
    public async Task Summary_labels_follow_the_request_language(string acceptLanguage, string[] expected)
    {
        var subject = await ResolveAsync(acceptLanguage);
        Assert.Equal(expected, subject.Summary.Select(l => l.Label).ToArray());
    }

    [Fact]
    public async Task One_task_has_the_same_subject_hash_and_fields_in_Turkish_and_English()
    {
        var turkish = await ResolveAsync("tr");
        var english = await ResolveAsync("en");

        Assert.NotEqual(turkish.Summary[0].Label, english.Summary[0].Label); // the labels did change …
        Assert.Equal(turkish.Fields, english.Fields);                         // … the canonical fields did not
        Assert.Equal(Hash(turkish), Hash(english));
        Assert.Equal(turkish.Display, english.Display);
        Assert.Equal(turkish.Summary.Select(l => l.Value), english.Summary.Select(l => l.Value));
    }
}

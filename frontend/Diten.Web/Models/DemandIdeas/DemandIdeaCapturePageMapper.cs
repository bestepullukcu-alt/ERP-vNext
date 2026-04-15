namespace Diten.Web.Models.DemandIdeas;

public static class DemandIdeaCapturePageMapper
{
    public static DemandIdeaCapturePageViewModel CreateNew(IReadOnlyList<DemandIdeaListItemDto> all)
    {
        var today = DateTime.UtcNow.Date;
        return new DemandIdeaCapturePageViewModel
        {
            PageMode = "create",
            RecordIdDisplay = "IC-2026-— (on save)",
            Status = "Draft",
            StatusBadgeCss = StatusBadgeClass("Draft"),
            RequestType = "Program",
            Priority = "Medium",
            BusinessUnit = "IT",
            StrategicAlignment = "Digital Transformation",
            DemandSource = "Internal",
            Category = "Program",
            ComplianceImpact = "None",
            EstimatedComplexity = "Medium",
            RiskSensitivity = "Medium",
            OwnerName = "You (sign-in user)",
            ReviewDueDate = today.AddDays(14),
            RelatedIdeas = PickRelated(null, all, 3),
            PossibleDuplicates = Array.Empty<DuplicateCardVm>(),
            LinkedStrategicThemes = new[] { "Digital Transformation", "Operational Excellence", "Portfolio Agility" },
            AssignedStakeholders = Array.Empty<StakeholderRowVm>(),
            SubmissionChecklist = DefaultSubmissionChecklist(false, false, false, true, false),
            RecentActivity = new[]
            {
                new ActivityLogItemVm("Capture workspace opened", "Just now", "bx-edit-alt"),
                new ActivityLogItemVm("Template applied: Standard demand", today.ToString("d"), "bx-file")
            },
            WorkflowTimeline = BuildTimeline(null, "Draft", false, true),
            DecisionHistory = Array.Empty<DecisionHistoryItemVm>(),
            NextStepChecklist = DefaultNextSteps(false),
            ShowTransferInSidebar = false
        };
    }

    public static DemandIdeaCapturePageViewModel FromItem(DemandIdeaListItemDto x, string pageMode, IReadOnlyList<DemandIdeaListItemDto> all)
    {
        var mode = pageMode.Equals("view", StringComparison.OrdinalIgnoreCase) ? "view" : "edit";
        var transferred = x.IsTransferred || x.Status == "Transferred";
        var readOnly = mode == "view";
        var canEdit = !readOnly && !transferred;
        var (problem, outcome, scope, benefits) = DemoNarrative(x.Title, x.Classification);
        var attachments = new List<CaptureAttachmentVm>();
        if (x.HasAttachments)
        {
            attachments.Add(new CaptureAttachmentVm("Business_Case_Summary.pdf", "240 KB"));
            attachments.Add(new CaptureAttachmentVm("Architecture_sketch.png", "128 KB"));
        }
        var links = new List<string>();
        if (x.HasSupportingLinks)
        {
            links.Add("https://intranet.example/policies/cost-governance");
            links.Add("https://wiki.example/teams/cloud-finops");
        }
        var stakeholders = new List<StakeholderRowVm>
        {
            new(x.RequestorName, Initials(x.RequestorName), "Requestor"),
            new(x.SponsorName, x.SponsorInitials, "Sponsor")
        };
        if (!string.IsNullOrEmpty(x.ReviewerName))
            stakeholders.Add(new(x.ReviewerName, Initials(x.ReviewerName), "Reviewer"));
        var dups = x.HasDuplicatesFlagged
            ? new[] { new DuplicateCardVm("DEM-2299", "Legacy data platform consolidation", "Similar scope and sponsor") }
            : Array.Empty<DuplicateCardVm>();
        var themes = new List<string> { "Digital Transformation", "Operational Excellence" };
        if (x.StrategicTheme == "Growth") themes.Insert(0, "Growth & Revenue");
        if (x.Classification == "Strategic") themes.Insert(0, "Strategic Portfolio");
        var showReviewer = readOnly || x.Status is "Under Review" or "Approved" or "Rejected" or "Transferred" or "Submitted";

        return new DemandIdeaCapturePageViewModel
        {
            PageMode = mode,
            Id = x.Id,
            RecordIdDisplay = ToDisplayRecordId(x.Id),
            StatusBadgeCss = StatusBadgeClass(x.Status),
            Title = x.Title,
            ProblemStatement = problem,
            ExpectedOutcome = outcome,
            RequestType = x.RequestType,
            StrategicAlignment = x.Classification == "Strategic" ? "Growth & Revenue" : "Operational Excellence",
            BusinessUnit = x.BusinessUnit,
            Requestor = x.RequestorName,
            Sponsor = x.SponsorName,
            OwnerName = x.OwnerName,
            ProposedScope = scope.proposed,
            OutOfScope = scope.outScope,
            Assumptions = scope.assumptions,
            Constraints = scope.constraints,
            BusinessImpact = benefits.impact,
            ExpectedBenefits = benefits.expected,
            ValueNotes = benefits.notes,
            Category = x.Category,
            DemandSource = x.DemandSource,
            Priority = x.Priority,
            ComplianceImpact = x.ComplianceImpact,
            EstimatedComplexity = x.EstimatedComplexity,
            RiskSensitivity = x.RiskSensitivity,
            Classification = x.Classification,
            SupportingLinks = links,
            Attachments = attachments,
            Notes = $"Coordination notes for {x.Title}. Align with quarterly planning cycle.",
            Tags = x.Tags.ToList(),
            ReviewerComments = x.Status == "Under Review"
                ? "Please provide more detail on the expected ROI and timeline estimates."
                : x.Status == "Approved" ? "Approved pending funding slot Q3." : "",
            InternalNotes = "Internal: visible to PMO admins only in production.",
            ReviewerCommentTimestampLabel = x.Status == "Under Review" && x.LastActivityAt.HasValue
                ? x.LastActivityAt.Value.ToString("MMM d, yyyy 'at' h:mm tt")
                : x.SubmittedAt?.ToString("MMM d, yyyy 'at' h:mm tt"),
            ShowReviewerSection = showReviewer,
            Status = x.Status,
            ReviewDueDate = x.ReviewDueDate,
            DueDate = x.DueDate,
            CanTransfer = x.CanTransfer,
            CanEditFields = canEdit,
            CanSaveDraft = canEdit && x.Status is "Draft" or "Rejected" or "Submitted" or "Under Review",
            CanSubmit = canEdit && x.Status is "Draft" or "Rejected",
            CanAssignReviewer = canEdit && x.Status is "Draft" or "Submitted" or "Under Review",
            ShowApprove = !readOnly && x.Status == "Under Review",
            ShowReject = !readOnly && x.Status == "Under Review",
            ShowTransfer = !readOnly && (x.CanTransfer || x.Status == "Approved"),
            ShowExport = !readOnly,
            RelatedIdeas = PickRelated(x.Id, all, x.HasRelatedIdeas ? 4 : 3),
            PossibleDuplicates = dups,
            LinkedStrategicThemes = themes.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            AssignedStakeholders = stakeholders,
            SubmissionChecklist = DefaultSubmissionChecklist(
                !string.IsNullOrWhiteSpace(x.Title),
                problem.Length > 40,
                !string.IsNullOrEmpty(x.SponsorName),
                true,
                x.HasAttachments),
            RecentActivity = BuildActivity(x),
            WorkflowTimeline = BuildTimeline(x, x.Status, transferred, false),
            DecisionHistory = BuildDecisions(x),
            NextStepChecklist = DefaultNextSteps(x.HasAttachments),
            ShowTransferInSidebar = transferred,
            Transfer = transferred
                ? new TransferBlockVm(
                    x.TransferStatus ?? "Completed",
                    x.TransferTargetType,
                    x.TransferTargetId ?? "",
                    x.TransferDate,
                    x.TransferredBy,
                    "/TaskReports/TaskReport")
                : null
        };
    }

    private static string ToDisplayRecordId(string id) => $"IC-2026-{id.Replace("DEM-", "", StringComparison.OrdinalIgnoreCase).Trim()}";

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    private static IReadOnlyList<RelatedIdeaCardVm> PickRelated(string? excludeId, IReadOnlyList<DemandIdeaListItemDto> all, int take) =>
        all.Where(i => i.Id != excludeId).Take(take).Select((i, idx) => new RelatedIdeaCardVm(i.Id, i.Title, 78 - idx * 6)).ToList();

    private static (string problem, string outcome, (string proposed, string outScope, string assumptions, string constraints) scope, (string impact, string expected, string notes) benefits)
        DemoNarrative(string title, string classification)
    {
        var problem =
            $"Business units require a structured response to changing requirements around \"{title}\". " +
            "Current processes are fragmented, creating delays, rework, and inconsistent stakeholder communication.";
        var outcome =
            "Deliver a governed, measurable outcome aligned to portfolio priorities with clear ownership, " +
            "success metrics, and a path to PPM transfer once approved.";
        var scope = (
            proposed: $"In scope: initiatives directly supporting {title}, including delivery teams, dependencies, and reporting cadence.",
            outScope: "Out of scope: unrelated legacy maintenance, ad-hoc shadow IT purchases, and non-approved vendor contracts.",
            assumptions: "Sponsor commitment through two planning cycles; architecture review available within 10 business days.",
            constraints: "Must comply with enterprise security baselines; budget subject to annual capex gate.");
        var benefits = (
            impact: classification == "Strategic" ? "Enterprise / multi-BU" : "Departmental",
            expected: "Reduced cycle time, improved transparency, and clearer prioritization versus status quo.",
            notes: "Quantify ROI where possible; link benefits to OKRs or KPIs in the next revision.");
        return (problem, outcome, scope, benefits);
    }

    private static IReadOnlyList<SubmissionCheckItemVm> DefaultSubmissionChecklist(bool titleOk, bool descOk, bool sponsorOk, bool priorityOk, bool docsOk) =>
        new[]
        {
            new SubmissionCheckItemVm("title", "Title and description complete", titleOk && descOk),
            new SubmissionCheckItemVm("case", "Business case defined", descOk),
            new SubmissionCheckItemVm("sponsor", "Sponsor identified", sponsorOk),
            new SubmissionCheckItemVm("priority", "Priority assigned", priorityOk),
            new SubmissionCheckItemVm("docs", "Supporting documents attached", docsOk)
        };

    private static IReadOnlyList<NextStepCardVm> DefaultNextSteps(bool docs) =>
        new[]
        {
            new NextStepCardVm("Complete required fields", "All fields marked * must be filled before submission.", false),
            new NextStepCardVm("Attach supporting documents", "Upload policy, estimates, or architecture references.", docs),
            new NextStepCardVm("Submit for review", "Notify the governance queue and assign a reviewer.", false)
        };

    private static IReadOnlyList<ActivityLogItemVm> BuildActivity(DemandIdeaListItemDto x)
    {
        var list = new List<ActivityLogItemVm>
        {
            new("Idea record created", "Created", "bx-plus-circle"),
            new("Draft saved", x.UpdatedAt?.ToString("d") ?? "—", "bx-save")
        };
        if (x.SubmittedAt.HasValue)
            list.Add(new("Submitted for review", x.SubmittedAt.Value.ToString("d"), "bx-send"));
        if (x.Status == "Under Review")
            list.Add(new("Comment added", $"{x.ReviewerName ?? "Jane Doe"} • {x.LastActivityAt?.ToString("g") ?? "—"}", "bx-message-dots"));
        if (x.IsTransferred)
            list.Add(new("Transferred to PPM", x.TransferDate?.ToString("d") ?? "—", "bx-export"));
        return list;
    }

    private static IReadOnlyList<WorkflowTimelineItemVm> BuildTimeline(DemandIdeaListItemDto? x, string status, bool transferred, bool isCreate)
    {
        var pastSubmit = status is "Under Review" or "Approved" or "Rejected" or "Transferred";
        var reviewOrLater = status is "Under Review" or "Approved" or "Rejected" or "Transferred";
        var pastReview = status is "Approved" or "Rejected" or "Transferred";
        var decisionDone = status is "Approved" or "Rejected" or "Transferred";
        const string pendingMeta = "Pending • TBD";

        if (isCreate || x is null)
        {
            var today = DateTime.UtcNow.Date;
            return new[]
            {
                new WorkflowTimelineItemVm("created", "Idea Created", true, false, $"{today:d} • You", false),
                new WorkflowTimelineItemVm("draft", "Draft Saved", false, true, "In progress • You", false),
                new WorkflowTimelineItemVm("submitted", "Submitted for review", false, false, null, true),
                new WorkflowTimelineItemVm("review", "Under Review", false, false, null, true),
                new WorkflowTimelineItemVm("decision", "Approval Decision", false, false, null, true),
                new WorkflowTimelineItemVm("transferred", "Transferred", false, false, null, true)
            };
        }

        var owner = string.IsNullOrEmpty(x.OwnerName) ? "Owner" : x.OwnerName;
        var created = x.CreatedAt?.ToString("d") ?? "—";
        var updated = x.UpdatedAt?.ToString("d") ?? "—";
        var submitted = x.SubmittedAt?.ToString("d") ?? "—";
        var reviewer = string.IsNullOrEmpty(x.ReviewerName) ? "Reviewer" : x.ReviewerName;
        var reviewWhen = x.LastActivityAt?.ToString("d") ?? submitted;

        return new[]
        {
            new WorkflowTimelineItemVm("created", "Idea Created", true, false, $"{created} • {owner}", false),
            new WorkflowTimelineItemVm("draft", "Draft Saved", status != "Draft", status == "Draft",
                status == "Draft" ? $"In progress • {owner}" : $"{updated} • {owner}", false),
            new WorkflowTimelineItemVm("submitted", "Submitted for review", pastSubmit, status == "Submitted",
                pastSubmit || status == "Submitted" ? $"{submitted} • {owner}" : null, status == "Draft"),
            new WorkflowTimelineItemVm("review", "Under Review", pastReview, status == "Under Review",
                reviewOrLater ? $"{reviewWhen} • {reviewer}" : null, !reviewOrLater),
            new WorkflowTimelineItemVm("decision", "Approval Decision", decisionDone, false,
                decisionDone
                    ? (status == "Rejected" ? $"{x.SubmittedAt?.AddDays(4):d} • Governance" : $"{x.SubmittedAt?.AddDays(5):d} • PMO")
                    : pendingMeta,
                !decisionDone),
            new WorkflowTimelineItemVm("transferred", "Transferred", transferred, false,
                transferred ? $"{x.TransferDate:d} • {x.TransferredBy ?? "PMO"}" : null,
                !transferred && status != "Transferred")
        };
    }

    private static IReadOnlyList<DecisionHistoryItemVm> BuildDecisions(DemandIdeaListItemDto x)
    {
        if (x.Status == "Approved")
            return new[] { new DecisionHistoryItemVm(x.SubmittedAt?.AddDays(5).ToString("d") ?? "—", "Portfolio Committee", "Approved", "Proceed to funding alignment.") };
        if (x.Status == "Rejected")
            return new[] { new DecisionHistoryItemVm(x.SubmittedAt?.AddDays(4).ToString("d") ?? "—", "Architecture Board", "Rejected", "Resubmit with security control matrix.") };
        if (x.Status == "Transferred")
            return new[]
            {
                new DecisionHistoryItemVm(x.TransferDate?.AddDays(-3).ToString("d") ?? "—", "PMO", "Approved", "Ready for PPM intake."),
                new DecisionHistoryItemVm(x.TransferDate?.ToString("d") ?? "—", x.TransferredBy ?? "PMO", "Transferred", $"Target: {x.TransferTargetType} {x.TransferTargetId}")
            };
        return Array.Empty<DecisionHistoryItemVm>();
    }

    private static string StatusBadgeClass(string status) =>
        status switch
        {
            "Draft" => "bg-label-secondary",
            "Submitted" => "bg-label-info text-info",
            "Under Review" => "bg-label-warning text-warning",
            "Approved" => "bg-label-success text-success",
            "Rejected" => "bg-label-danger text-danger",
            "Transferred" => "bg-label-primary text-primary",
            _ => "bg-label-secondary"
        };
}

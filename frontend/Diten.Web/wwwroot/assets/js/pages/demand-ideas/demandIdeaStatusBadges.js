(() => {
    const statusBadgeMap = {
        Draft: "bg-label-secondary text-secondary",
        Submitted: "bg-label-info text-info",
        "Under Review": "bg-label-warning text-warning",
        Approved: "bg-label-success text-success",
        Rejected: "bg-label-danger text-danger",
        Transferred: "bg-label-primary text-primary"
    };
    const priorityBadgeMap = {
        Low: "bg-label-secondary text-secondary",
        Medium: "bg-label-info text-info",
        High: "bg-label-warning text-warning",
        Critical: "bg-label-danger text-danger"
    };
    window.DemandIdeaStatusBadges = {
        statusClass(s) { return statusBadgeMap[s] || "bg-label-secondary text-secondary"; },
        priorityClass(p) { return priorityBadgeMap[p] || "bg-label-secondary text-secondary"; }
    };
})();

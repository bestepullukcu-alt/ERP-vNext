using System.Collections.Generic;

namespace Diten.Application.Responses;

public class TaskReportSummaryResponse
{
    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public int PendingCount { get; set; }
    public double CompletionRate { get; set; }
    public List<int> ChartData { get; set; } = new();
    public List<string> ChartLabels { get; set; } = new();
}

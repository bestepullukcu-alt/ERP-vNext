namespace Diten.Web.Models.DataTable;

public sealed class DataTableBulkActionBarViewModel
{
    public string Id { get; set; } = "bulkActionBar";
    public string CountId { get; set; } = "bulkSelectedCount";
    public string ClearSelectionId { get; set; } = "btnClearSelection";
    public string SelectedText { get; set; } = "Selected";
    public string ClearSelectionText { get; set; } = "Clear";
    public string Icon { get; set; } = "bx bx-check-circle";
    public string IconClass { get; set; } = "text-primary";
    public IReadOnlyList<DataTableActionItemViewModel> Actions { get; set; } = [];
}

public sealed class DataTableActionItemViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ButtonClass { get; set; } = "btn btn-label-secondary";
    public bool Visible { get; set; } = true;
    public bool RequiresSelection { get; set; } = true;
    public string? ConfirmKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Method { get; set; }
}

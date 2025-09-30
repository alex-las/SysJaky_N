namespace SysJaky_N.Pages.Admin.Shared;

public class FormActionsModel
{
    public string SubmitText { get; set; } = "Save";

    public string SubmitCssClass { get; set; } = "btn btn-primary";

    public string CancelPage { get; set; } = "Index";

    public string CancelText { get; set; } = "Cancel";

    public string CancelCssClass { get; set; } = "btn btn-secondary";

    public bool ShowCancel { get; set; } = true;
}

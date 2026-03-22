namespace InvoiceWizard.Data.ViewModels;

public class ProjectSelectionItem
{
    public int? ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string ProjectStatus { get; set; } = "Active";

    public string DisplayName => ProjectId is null
        ? Name
        : ProjectStatus switch
        {
            "Paused" => $"{Name} (Pausiert)",
            "Ended" => $"{Name} (Beendet)",
            _ => Name
        };
}

using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.ViewModels.Workspaces;

public class CreateWorkspaceViewModel
{
    [Required]
    [StringLength(120)]
    [Display(Name = "Workspace name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }
}

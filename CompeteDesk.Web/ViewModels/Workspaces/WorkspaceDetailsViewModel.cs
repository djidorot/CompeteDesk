using System;
using System.Collections.Generic;
using CompeteDesk.Models;

namespace CompeteDesk.ViewModels.Workspaces;

public sealed class WorkspaceDetailsViewModel
{
    public Workspace Workspace { get; set; } = new();

    public List<WorkspaceMemberRow> Members { get; set; } = new();
    public List<WorkspaceInviteRow> Invites { get; set; } = new();
    public List<WorkspaceStrategyRow> Strategies { get; set; } = new();
    public List<WorkspaceActivityRow> Activity { get; set; } = new();

    public int StrategyCount { get; set; }
    public int ActiveStrategyCount { get; set; }
    public int CommentCount { get; set; }
}

public sealed class WorkspaceMemberRow
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public DateTime JoinedAtUtc { get; set; }
    public bool IsOwner { get; set; }
}

public sealed class WorkspaceInviteRow
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class WorkspaceStrategyRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CommentCount { get; set; }
}

public sealed class WorkspaceActivityRow
{
    public DateTime CreatedAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ActorEmail { get; set; }
}

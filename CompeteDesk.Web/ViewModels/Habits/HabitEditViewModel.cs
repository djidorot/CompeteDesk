using System.Collections.Generic;
using CompeteDesk.Models;

namespace CompeteDesk.ViewModels.Habits;

public class HabitEditViewModel
{
    public Habit Habit { get; set; } = new Habit();
    public List<(int Id, string Name)> Workspaces { get; set; } = new();
    public List<(int Id, string Name)> Strategies { get; set; } = new();
}

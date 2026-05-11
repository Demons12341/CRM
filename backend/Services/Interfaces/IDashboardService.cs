using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardOverviewDto> GetOverviewAsync(int userId);
        Task<List<TaskDto>> GetMyTasksAsync(int userId);
        Task<List<MyProjectDto>> GetMyProjectsAsync(int userId);
    }

    public class DashboardOverviewDto
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int TotalTasks { get; set; }
        public Dictionary<int, int> ProjectStatusCounts { get; set; } = new();
        public Dictionary<int, int> TaskStatusCounts { get; set; } = new();
        public List<PendingContractProjectDto> PendingContractProjects { get; set; } = new();
        public List<ProgressProjectDto> ProgressProjects { get; set; } = new();
        public List<UpcomingOverdueItemDto> UpcomingOverdueItems { get; set; } = new();
    }

    public class PendingContractProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BusinessLine { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public decimal? Budget { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ProgressProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BusinessLine { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime? StatusChangedAt { get; set; }
        public string ManagerName { get; set; } = string.Empty;
    }

    public class UpcomingOverdueItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public int DaysLeft { get; set; }
        public bool IsOverdue { get; set; }
        public string AssigneeName { get; set; } = string.Empty;
    }

    public class MyProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BusinessLine { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public decimal Progress { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public bool IsManager { get; set; }
    }
}

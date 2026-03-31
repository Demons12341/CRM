using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardOverviewDto> GetOverviewAsync(int userId);
        Task<List<TaskDto>> GetMyTasksAsync(int userId);
    }

    public class DashboardOverviewDto
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int TotalTasks { get; set; }
        public int OverdueTasks { get; set; }
    }
}

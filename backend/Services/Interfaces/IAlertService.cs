using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IAlertService
    {
        Task<PaginatedResult<AlertDto>> GetAlertsAsync(int userId, int page, int pageSize, int? alertType, bool? isRead, int? alertStatus);
        Task<int> GetUnreadCountAsync(int userId);
        Task<bool> MarkAsReadAsync(int id, int userId);
        Task<bool> MarkAllAsReadAsync(int userId);
        Task<bool> UpdateOverdueReasonAsync(int alertId, int userId, string? overdueReason);
        Task CheckOverdueTasksAsync();
        Task CheckOverdueProjectsAsync();
    }

    public class AlertDto
    {
        public int Id { get; set; }
        public int? ProjectId { get; set; }
        public int? ProjectManagerId { get; set; }
        public string? ProjectName { get; set; }
        public string? ProjectManagerName { get; set; }
        public int? TaskId { get; set; }
        public string? TaskName { get; set; }
        public string? TaskAssigneeName { get; set; }
        public int UserId { get; set; }
        public int AlertType { get; set; }
        public string AlertTypeName { get; set; } = string.Empty;
        public int AlertStatus { get; set; }
        public string AlertStatusName { get; set; } = string.Empty;
        public string? OverdueReason { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
